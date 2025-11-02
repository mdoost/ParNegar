using ParNegar.Shared.DTOs.Auth;

namespace ParNegar.Application.Interfaces.Services.Auth;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(
        LoginRequestDto request,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<int> GetActiveSessionsCountAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task<List<SessionDto>> GetActiveSessionsAsync(
        long userId,
        string? currentSessionId = null,
        CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task RevokeAllSessionsAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task RevokeAllSessionsExceptCurrentAsync(
        long userId,
        string currentSessionId,
        CancellationToken cancellationToken = default);
}
