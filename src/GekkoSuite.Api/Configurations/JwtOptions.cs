namespace GekkoSuite.Api.Configurations;

/// <summary>
/// The signing settings for the access token
/// </summary>
public class JwtOptions
{
    /// <summary>The symmetric signing key. Must be long/random enough for HMAC-SHA256 (32+ bytes).</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>The token's "iss" claim; checked on validation so a token from elsewhere is rejected.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>The token's "aud" claim; checked on validation so a token meant for another audience is rejected.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>How long an access token stays valid, in minutes.</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
