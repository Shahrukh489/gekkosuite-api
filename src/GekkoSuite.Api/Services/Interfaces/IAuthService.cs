namespace GekkoSuite.Api.Services;

public class LoginResponse
{
    /// <summary>The signed JWT the client sends as "Authorization: Bearer {accessToken}" on every request.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Seconds until the access token expires.</summary>
    public int ExpiresIn { get; set; }
}

public interface IAuthService
{
    /// <summary>
    /// Verifies an email + password against the stored account and, if the credentials are valid and the
    /// account is active, issues a signed access token.
    /// </summary>
    /// <param name="email">The login email from the request body.</param>
    /// <param name="password">The login password from the request body.</param>
    /// <returns>
    /// The issued token, or null if login should be refused
    /// </returns>
    Task<LoginResponse?> LoginAsync(string email, string password);
}
