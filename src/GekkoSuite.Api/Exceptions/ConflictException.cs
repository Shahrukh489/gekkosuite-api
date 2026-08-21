namespace GekkoSuite.Api.Exceptions;

/// <summary>
/// The request conflicts with an existing record (e.g. user_account.email's UNIQUE constraint).
/// </summary>
public class ConflictException : Exception
{
    /// <summary>
    /// Constructor that accepts only a localized message.
    /// </summary>
    public ConflictException(string? message) : base(message)
    {
    }
}
