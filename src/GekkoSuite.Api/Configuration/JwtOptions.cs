namespace GekkoSuite.Api.Configuration;

/// <summary>
/// The signing settings for the access token issued by POST /auth/login. Read once at startup from
/// environment variables (see Program.cs) — never hardcoded, and never the same secret across
/// environments. A single symmetric key both issues and (later) validates the token, since this API is
/// the only party that ever needs to do either.
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
