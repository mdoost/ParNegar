using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using ParNegar.Application.Interfaces.Services;
using ParNegar.Application.Interfaces.Services.Auth;
using ParNegar.Domain.Entities.Auth;
using ParNegar.Domain.Interfaces;
using ParNegar.Shared.DTOs.Common;
using ParNegar.Shared.Exceptions;

namespace ParNegar.Infrastructure.Services.Auth;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly IRepository<RefreshToken> _refreshTokenRepository;
    private readonly IRepository<TokenBlacklist> _blacklistRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTime _dateTime;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        IConfiguration configuration,
        IRepository<RefreshToken> refreshTokenRepository,
        IRepository<TokenBlacklist> blacklistRepository,
        IUnitOfWork unitOfWork,
        IDateTime dateTime,
        ILogger<TokenService> logger)
    {
        _configuration = configuration;
        _refreshTokenRepository = refreshTokenRepository;
        _blacklistRepository = blacklistRepository;
        _unitOfWork = unitOfWork;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task<TokenResponseDto> GenerateTokenAsync(
        User user,
        string ipAddress,
        string userAgent,
        string? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        var jwtId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        // Generate Access Token
        var accessToken = GenerateJwtToken(user, jwtId, sessionId, now);

        // Generate Refresh Token
        var refreshTokenString = GenerateRefreshTokenString();

        // Save refresh token to database
        var refreshToken = new RefreshToken
        {
            Token = refreshTokenString,
            JwtId = jwtId,
            SessionId = sessionId,
            UserID = user.ID,
            ExpiresAt = _dateTime.OffsetUtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7")),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceId = deviceId,
            IsUsed = false,
            IsRevoked = false
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Token generated for user {Username} from IP {IpAddress}",
            user.Username, ipAddress);

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresIn = int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "60") * 60,
            TokenType = "Bearer"
        };
    }

    public async Task<TokenResponseDto> RefreshTokenAsync(
        string accessToken,
        string refreshToken,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default)
    {
        // Validate the expired access token
        var principal = GetPrincipalFromExpiredToken(accessToken);
        var jwtId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(jwtId) || string.IsNullOrEmpty(userIdClaim))
        {
            throw new UnauthorizedException("Invalid token");
        }

        var userId = long.Parse(userIdClaim);

        // Find refresh token in database
        var storedToken = await _refreshTokenRepository.Query()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.JwtId == jwtId, cancellationToken);

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh token not found for user {UserId}", userId);
            throw new UnauthorizedException("Invalid refresh token");
        }

        // Validate refresh token
        if (storedToken.IsUsed || storedToken.IsRevoked)
        {
            _logger.LogWarning("Refresh token reuse detected for user {UserId}. Revoking all tokens.", userId);
            await RevokeAllUserTokensAsync(userId, "Token reuse detected", cancellationToken);
            throw new UnauthorizedException("Invalid refresh token");
        }

        if (storedToken.ExpiresAt <= _dateTime.OffsetUtcNow)
        {
            throw new UnauthorizedException("Refresh token expired");
        }

        // Mark old refresh token as used
        storedToken.IsUsed = true;
        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Generate new tokens
        return await GenerateTokenAsync(storedToken.User!, ipAddress, userAgent, storedToken.DeviceId, cancellationToken);
    }

    public async Task RevokeTokenAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
    {
        var tokens = await _refreshTokenRepository.Query()
            .Where(rt => rt.SessionId == sessionId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            await _refreshTokenRepository.UpdateAsync(token, cancellationToken);
        }

        // Add to blacklist
        var blacklist = new TokenBlacklist
        {
            SessionId = sessionId,
            UserId = tokens.FirstOrDefault()?.UserID ?? 0,
            BlacklistedAt = _dateTime.OffsetUtcNow,
            ExpiresAt = _dateTime.OffsetUtcNow.AddDays(7),
            Reason = reason,
            Type = "Session"
        };

        await _blacklistRepository.AddAsync(blacklist, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session {SessionId} revoked: {Reason}", sessionId, reason);
    }

    public async Task RevokeAllUserTokensAsync(long userId, string reason, CancellationToken cancellationToken = default)
    {
        var tokens = await _refreshTokenRepository.Query()
            .Where(rt => rt.UserID == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            await _refreshTokenRepository.UpdateAsync(token, cancellationToken);

            // Add to blacklist
            if (!string.IsNullOrEmpty(token.SessionId))
            {
                var blacklist = new TokenBlacklist
                {
                    SessionId = token.SessionId,
                    UserId = userId,
                    BlacklistedAt = _dateTime.OffsetUtcNow,
                    ExpiresAt = _dateTime.OffsetUtcNow.AddDays(7),
                    Reason = reason,
                    Type = "Session"
                };

                await _blacklistRepository.AddAsync(blacklist, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("All tokens revoked for user {UserId}: {Reason}", userId, reason);
    }

    public async Task RevokeAllUserTokensExceptCurrentAsync(
        long userId,
        string currentSessionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _refreshTokenRepository.Query()
            .Where(rt => rt.UserID == userId && rt.SessionId != currentSessionId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            await _refreshTokenRepository.UpdateAsync(token, cancellationToken);

            // Add to blacklist
            if (!string.IsNullOrEmpty(token.SessionId))
            {
                var blacklist = new TokenBlacklist
                {
                    SessionId = token.SessionId,
                    UserId = userId,
                    BlacklistedAt = _dateTime.OffsetUtcNow,
                    ExpiresAt = _dateTime.OffsetUtcNow.AddDays(7),
                    Reason = reason,
                    Type = "Session"
                };

                await _blacklistRepository.AddAsync(blacklist, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("All tokens except current revoked for user {UserId}", userId);
    }

    public async Task<bool> IsSessionBlacklistedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var blacklisted = await _blacklistRepository.Query()
            .AnyAsync(bl => bl.SessionId == sessionId && bl.ExpiresAt > _dateTime.OffsetUtcNow, cancellationToken);

        return blacklisted;
    }

    private string GenerateJwtToken(User user, string jwtId, string sessionId, DateTime now)
    {
        var secret = _configuration["JwtSettings:Secret"]!;
        var issuer = _configuration["JwtSettings:Issuer"]!;
        var audience = _configuration["JwtSettings:Audience"]!;
        var expirationMinutes = int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti, jwtId),
            new Claim("session_id", sessionId),
            new Claim("branch_id", user.BranchID.ToString())
        };

        // Add roles (if user has roles loaded)
        if (user.UserRoles != null && user.UserRoles.Any())
        {
            foreach (var userRole in user.UserRoles.Where(ur => ur.IsActive))
            {
                if (userRole.SystemRole != null)
                {
                    claims.Add(new Claim(ClaimTypes.Role, userRole.SystemRole.Code));
                }
            }
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddMinutes(expirationMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials,
            NotBefore = now
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    private static string GenerateRefreshTokenString()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var secret = _configuration["JwtSettings:Secret"]!;

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["JwtSettings:Issuer"],
            ValidAudience = _configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = false // We're validating expired tokens
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new UnauthorizedException("Invalid token");
        }

        return principal;
    }
}
