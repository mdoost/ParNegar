using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParNegar.Application.Interfaces.Services;
using ParNegar.Application.Interfaces.Services.Auth;
using ParNegar.Domain.Entities.Auth;
using ParNegar.Domain.Interfaces;
using ParNegar.Shared.DTOs.Auth;
using ParNegar.Shared.Exceptions;
using ParNegar.Shared.Utilities;

namespace ParNegar.Infrastructure.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserLoginLog> _loginLogRepository;
    private readonly IRepository<RefreshToken> _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTime _dateTime;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IRepository<User> userRepository,
        IRepository<UserLoginLog> loginLogRepository,
        IRepository<RefreshToken> refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IDateTime dateTime,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _loginLogRepository = loginLogRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task<LoginResponseDto> LoginAsync(
        LoginRequestDto request,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default)
    {
        // Find user
        var user = await _userRepository.Query()
            .Include(u => u.Branch)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.SystemRole)
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user == null)
        {
            await LogFailedLoginAttempt(request.Username, ipAddress, userAgent, "User not found", cancellationToken);
            throw new UnauthorizedException("Invalid username or password");
        }

        // Check if user is active
        if (!user.IsActive)
        {
            await LogFailedLoginAttempt(request.Username, ipAddress, userAgent, "User is not active", cancellationToken);
            throw new UnauthorizedException("Account is not active");
        }

        // Check if user is locked
        if (user.IsLocked)
        {
            await LogFailedLoginAttempt(request.Username, ipAddress, userAgent, "User is locked", cancellationToken);
            throw new UnauthorizedException("Account is locked");
        }

        // Verify password
        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            // Increment failed login attempts
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts == 1)
            {
                user.FirstFailedLoginDate = _dateTime.OffsetUtcNow;
                user.sFirstFailedLoginDate = PersianDateHelper.ConvertToPersianDateSafe(user.FirstFailedLoginDate);
            }

            // Lock account after 5 failed attempts
            if (user.FailedLoginAttempts >= 5)
            {
                user.IsLocked = true;
                _logger.LogWarning("User {Username} locked after {Attempts} failed attempts", user.Username, user.FailedLoginAttempts);
            }

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await LogFailedLoginAttempt(request.Username, ipAddress, userAgent, "Invalid password", cancellationToken);
            throw new UnauthorizedException("Invalid username or password");
        }

        // Reset failed login attempts on successful login
        user.FailedLoginAttempts = 0;
        user.FirstFailedLoginDate = null;
        user.sFirstFailedLoginDate = null;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Generate tokens
        var tokenResponse = await _tokenService.GenerateTokenAsync(
            user,
            ipAddress,
            userAgent,
            request.DeviceId,
            cancellationToken);

        // Log successful login
        await LogSuccessfulLogin(user, ipAddress, userAgent, tokenResponse, cancellationToken);

        _logger.LogInformation("User {Username} logged in successfully from IP {IpAddress}", user.Username, ipAddress);

        return new LoginResponseDto
        {
            Token = tokenResponse,
            User = MapUserToDto(user)
        };
    }

    public async Task LogoutAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // Find refresh token by session ID
        var refreshToken = await _refreshTokenRepository.Query()
            .FirstOrDefaultAsync(rt => rt.SessionId == sessionId && !rt.IsRevoked, cancellationToken);

        if (refreshToken == null)
        {
            _logger.LogWarning("Session {SessionId} not found or already revoked", sessionId);
            return;
        }

        // Update login log
        var loginLog = await _loginLogRepository.Query()
            .FirstOrDefaultAsync(l => l.SessionID == sessionId && l.LogoutDate == null, cancellationToken);

        if (loginLog != null)
        {
            loginLog.LogoutDate = _dateTime.OffsetUtcNow;
            loginLog.sLogoutDate = PersianDateHelper.ConvertToPersianDateSafe(loginLog.LogoutDate);
            await _loginLogRepository.UpdateAsync(loginLog, cancellationToken);
        }

        // Revoke token
        await _tokenService.RevokeTokenAsync(sessionId, "User logout", cancellationToken);

        _logger.LogInformation("Session {SessionId} logged out", sessionId);
    }

    public async Task<int> GetActiveSessionsCountAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await _refreshTokenRepository.Query()
            .Where(rt => rt.UserID == userId && !rt.IsRevoked && !rt.IsUsed && rt.ExpiresAt > _dateTime.OffsetUtcNow)
            .CountAsync(cancellationToken);
    }

    public async Task<List<SessionDto>> GetActiveSessionsAsync(
        long userId,
        string? currentSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _refreshTokenRepository.Query()
            .Where(rt => rt.UserID == userId && !rt.IsRevoked && !rt.IsUsed && rt.ExpiresAt > _dateTime.OffsetUtcNow)
            .OrderByDescending(rt => rt.CreatedDate)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new SessionDto
        {
            ID = s.ID,
            GUID = s.GUID,
            SessionId = s.SessionId ?? string.Empty,
            IpAddress = s.IpAddress,
            UserAgent = s.UserAgent,
            DeviceId = s.DeviceId,
            CreatedDate = s.CreatedDate,
            ExpiresAt = s.ExpiresAt,
            IsCurrentSession = s.SessionId == currentSessionId,
            IsValid = !s.IsRevoked && !s.IsUsed
        }).ToList();
    }

    public async Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _tokenService.RevokeTokenAsync(sessionId, "Session revoked by user", cancellationToken);
    }

    public async Task RevokeAllSessionsAsync(long userId, CancellationToken cancellationToken = default)
    {
        await _tokenService.RevokeAllUserTokensAsync(userId, "All sessions revoked by user", cancellationToken);
    }

    public async Task RevokeAllSessionsExceptCurrentAsync(
        long userId,
        string currentSessionId,
        CancellationToken cancellationToken = default)
    {
        await _tokenService.RevokeAllUserTokensExceptCurrentAsync(
            userId,
            currentSessionId,
            "Other sessions revoked by user",
            cancellationToken);
    }

    private async Task LogSuccessfulLogin(
        User user,
        string ipAddress,
        string userAgent,
        ParNegar.Shared.DTOs.Common.TokenResponseDto tokenResponse,
        CancellationToken cancellationToken)
    {
        // Extract session ID from refresh token (we need to get it from the database)
        var refreshToken = await _refreshTokenRepository.Query()
            .Where(rt => rt.Token == tokenResponse.RefreshToken)
            .FirstOrDefaultAsync(cancellationToken);

        var loginLog = new UserLoginLog
        {
            UserID = user.ID,
            Username = user.Username,
            IPAddress = ipAddress,
            UserAgent = userAgent,
            LoginType = "Password",
            IsSuccessful = true,
            SessionID = refreshToken?.SessionId,
            LoginDate = _dateTime.OffsetUtcNow,
            sLoginDate = PersianDateHelper.ConvertToPersianDateSafe(_dateTime.OffsetUtcNow) ?? string.Empty
        };

        await _loginLogRepository.AddAsync(loginLog, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task LogFailedLoginAttempt(
        string username,
        string ipAddress,
        string userAgent,
        string reason,
        CancellationToken cancellationToken)
    {
        var loginLog = new UserLoginLog
        {
            Username = username,
            IPAddress = ipAddress,
            UserAgent = userAgent,
            LoginType = "Password",
            IsSuccessful = false,
            FailureReason = reason,
            LoginDate = _dateTime.OffsetUtcNow,
            sLoginDate = PersianDateHelper.ConvertToPersianDateSafe(_dateTime.OffsetUtcNow) ?? string.Empty
        };

        await _loginLogRepository.AddAsync(loginLog, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static UserDto MapUserToDto(User user)
    {
        var dto = user.Adapt<UserDto>();
        dto.BranchName = user.Branch?.NameFa ?? string.Empty;
        return dto;
    }
}
