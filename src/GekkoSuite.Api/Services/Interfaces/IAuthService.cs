using GekkoSuite.Api.Dtos.Auth;

namespace GekkoSuite.Api.Services;

/// <summary>
/// The three things a login attempt can end in. Kept as a plain enum (not an HTTP status) so AuthService
/// stays ignorant of HTTP — the controller is the only place that maps an outcome to a status code.
/// </summary>
public enum LoginOutcome
{
    /// <summary>Credentials checked out and the account is active; Response on the result is set.</summary>
    Success,

    /// <summary>No live account matched the email, or the password was wrong. Deliberately the SAME
    /// outcome for both cases (see auth.md's Security Review, R12) so a client can never learn which one
    /// it was — that would let an attacker enumerate valid accounts.</summary>
    InvalidCredentials,

    /// <summary>The email/password matched, but the account is disabled (user.is_active = false).</summary>
    AccountDisabled
}

/// <summary>
/// The result of a login attempt. Response is only populated when Outcome is Success.
/// </summary>
public class LoginResult
{
    public LoginOutcome Outcome { get; set; }

    public LoginResponse? Response { get; set; }
}

public interface IAuthService
{
    /// <summary>
    /// Verifies an email + password against the stored account and, if the credentials are valid and the
    /// account is active, issues a signed access token.
    /// </summary>
    /// <param name="request">The login credentials from the request body.</param>
    /// <returns>The outcome of the attempt, and the issued token when it succeeded.</returns>
    Task<LoginResult> LoginAsync(LoginRequest request);
}
