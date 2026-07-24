namespace GekkoSuite.Api.Exceptions;

public class ValidationException : Exception
{
    /// <summary>
    /// Constructor that accepts only a localized message
    /// </summary>
    public ValidationException(string? message) : base(message)
    {
    }

    /// <summary>
    ///  Constructor that accepts a localized message and an inner exception
    /// </summary>
    public ValidationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
