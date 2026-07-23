namespace GekkoSuite.Api.Services;

public class LoginResponse
{
    /// <summary>The signed JWT.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Seconds until the access token expires.</summary>
    public int ExpiresIn { get; set; }
}

public interface IAuthService
{
    /// <summary>
    /// Verifies an email + password against the stored credentials
    /// </summary>
    Task<LoginResponse?> LoginAsync(string email, string password);
}
