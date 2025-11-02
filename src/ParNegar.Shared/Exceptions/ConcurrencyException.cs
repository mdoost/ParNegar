namespace ParNegar.Shared.Exceptions;

/// <summary>
/// Exception thrown when concurrent update conflict occurs
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException() : base("Concurrency conflict occurred") { }

    public ConcurrencyException(string message) : base(message) { }

    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}
