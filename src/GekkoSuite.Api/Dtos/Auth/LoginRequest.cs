namespace GekkoSuite.Api.Dtos.Auth;

/// <summary>
/// Request body for POST /auth/login (docs/api.md). Both fields are required.
/// </summary>
public class LoginRequest
{
    /// <summary>The account's login email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The account's plaintext password, checked against the stored Argon2id hash.</summary>
    public string Password { get; set; } = string.Empty;
}
