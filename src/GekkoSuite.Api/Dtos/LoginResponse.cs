namespace GekkoSuite.Api.Dtos;
public class LoginResponse
{
    /// <summary>The signed JWT.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Seconds until the access token expires.</summary>
    public int ExpiresIn { get; set; }
}
