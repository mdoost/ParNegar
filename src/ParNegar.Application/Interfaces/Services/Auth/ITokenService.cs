using ParNegar.Domain.Entities.Auth;
using ParNegar.Shared.DTOs.Common;

namespace ParNegar.Application.Interfaces.Services.Auth;

public interface ITokenService
{
    Task<TokenResponseDto> GenerateTokenAsync(
        User user,
        string ipAddress,
        string userAgent,
        string? deviceId = null,
        CancellationToken cancellationToken = default);

    Task<TokenResponseDto> RefreshTokenAsync(
        string accessToken,
        string refreshToken,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default);

    Task RevokeTokenAsync(string sessionId, string reason, CancellationToken cancellationToken = default);

    Task RevokeAllUserTokensAsync(long userId, string reason, CancellationToken cancellationToken = default);

    Task RevokeAllUserTokensExceptCurrentAsync(
        long userId,
        string currentSessionId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<bool> IsSessionBlacklistedAsync(string sessionId, CancellationToken cancellationToken = default);
}
