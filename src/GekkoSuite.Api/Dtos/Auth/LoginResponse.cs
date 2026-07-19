namespace GekkoSuite.Api.Dtos.Auth;

/// <summary>
/// Response body for POST /auth/login (docs/api.md). The token carries only userId + organizationId —
/// never roles or permissions, so revocation stays immediate (see docs/auth.md).
/// </summary>
public class LoginResponse
{
    /// <summary>The signed JWT the client sends as "Authorization: Bearer {accessToken}" on every request.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Seconds until the access token expires.</summary>
    public int ExpiresIn { get; set; }
}
