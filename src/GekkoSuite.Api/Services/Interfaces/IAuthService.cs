using GekkoSuite.Api.Dtos.Auth;

namespace GekkoSuite.Api.Services;

public interface IAuthService
{
    /// <summary>
    /// Verifies an email + password against the stored account and, if the credentials are valid and the
    /// account is active, issues a signed access token.
    /// </summary>
    /// <param name="request">The login credentials from the request body.</param>
    /// <returns>
    /// The issued token, or null if login should be refused for any reason — no such email, wrong
    /// password, or a disabled account all look identical from the outside (see auth.md's Security
    /// Review, R12 — don't give an attacker a way to tell them apart).
    /// </returns>
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}
