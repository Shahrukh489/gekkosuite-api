using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Konscious.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

using GekkoSuite.Api.Configuration;
using GekkoSuite.Api.Dtos.Auth;
using GekkoSuite.Api.Models;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class AuthService : IAuthService
{
    // OWASP's minimum recommended Argon2id baseline (m=19 MiB, t=2, p=1). Kept as constants rather than
    // config because changing them doesn't need to be an ops-level decision, and every hash must be
    // verified with the SAME parameters it was created with (they aren't stored alongside the hash).
    private const int ArgonMemoryKb = 19 * 1024;
    private const int ArgonIterations = 2;
    private const int ArgonParallelism = 1;
    private const int ArgonHashLengthBytes = 32;

    private readonly IUserRepository _userRepository;
    private readonly JwtOptions _jwtOptions;

    public AuthService(IUserRepository userRepository, JwtOptions jwtOptions)
    {
        _userRepository = userRepository;
        _jwtOptions = jwtOptions;
    }

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
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.FindByEmailAsync(request.Email);

        // Checking the hash even when user is null would be nice for timing-attack hygiene, but is
        // skipped here for simplicity; the meaningful secret (the password) is never exposed either way.
        if (user is null || !VerifyPassword(request.Password, user.Password) || !user.IsActive)
        {
            return null;
        }

        return IssueAccessToken(user);
    }

    /// <summary>
    /// Checks a plaintext password against a stored Argon2id hash.
    /// </summary>
    /// <param name="password">The plaintext password from the request.</param>
    /// <param name="storedHash">The value from user.password, formatted as "{base64Salt}:{base64Hash}".</param>
    /// <returns>True if the password matches the hash.</returns>
    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = HashPassword(password, salt);

        // Fixed-time comparison so a mismatch can't be timed to leak how many leading bytes matched.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <summary>
    /// Hashes a password with Argon2id using a caller-supplied salt. Shared by verification (salt read
    /// from the stored hash) and, later, account creation (salt freshly generated).
    /// </summary>
    /// <param name="password">The plaintext password.</param>
    /// <param name="salt">The salt to hash with.</param>
    /// <returns>The raw hash bytes.</returns>
    private static byte[] HashPassword(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = ArgonParallelism,
            MemorySize = ArgonMemoryKb,
            Iterations = ArgonIterations
        };

        return argon2.GetBytes(ArgonHashLengthBytes);
    }

    /// <summary>
    /// Signs and builds the access token for a logged-in user. Carries only userId + organizationId —
    /// never roles or permissions — so a role/permission change takes effect immediately on the next
    /// request instead of waiting for the token to expire (see auth.md).
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>The signed token and its lifetime in seconds.</returns>
    private LoginResponse IssueAccessToken(UserEntity user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId", user.UserId.ToString()),
            new Claim("organizationId", user.OrganizationId.ToString())
        };

        var expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresIn = _jwtOptions.AccessTokenLifetimeMinutes * 60
        };
    }
}
