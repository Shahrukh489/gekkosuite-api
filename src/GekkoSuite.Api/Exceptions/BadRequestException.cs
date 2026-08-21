namespace GekkoSuite.Api.Exceptions;

/// <summary>
/// A client-input error a service rejects on purpose (missing/invalid field, a role that doesn't fit
/// the request). Distinct from ValidationException, which marks a server-side invariant that should
/// never be reachable from valid input — see UserDto.FromEntity's one-kind check.
/// </summary>
public class BadRequestException : Exception
{
    /// <summary>
    /// Constructor that accepts only a localized message.
    /// </summary>
    public BadRequestException(string? message) : base(message)
    {
    }
}
