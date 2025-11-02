namespace ParNegar.Shared.Exceptions;

/// <summary>
/// Exception thrown when business validation fails
/// </summary>
public class BusinessValidationException : Exception
{
    public Dictionary<string, string[]>? Errors { get; set; }

    public BusinessValidationException() : base("Business validation failed") { }

    public BusinessValidationException(string message) : base(message) { }

    public BusinessValidationException(string message, Exception innerException) : base(message, innerException) { }

    public BusinessValidationException(string message, Dictionary<string, string[]> errors) : base(message)
    {
        Errors = errors;
    }
}
