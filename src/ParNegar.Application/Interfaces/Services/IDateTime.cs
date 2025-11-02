namespace ParNegar.Application.Interfaces.Services;

/// <summary>
/// Interface for DateTime operations (for testing)
/// </summary>
public interface IDateTime
{
    DateTime Now { get; }
    DateTime UtcNow { get; }
    DateTimeOffset OffsetNow { get; }
    DateTimeOffset OffsetUtcNow { get; }
}
