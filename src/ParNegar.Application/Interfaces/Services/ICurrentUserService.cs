namespace ParNegar.Application.Interfaces.Services;

/// <summary>
/// Interface for accessing current user information
/// </summary>
public interface ICurrentUserService
{
    long? UserId { get; }
    string? Username { get; }
    string? Email { get; }
    string? SessionId { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
    IEnumerable<string> Roles { get; }
    bool HasRole(string role);
}
