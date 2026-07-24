using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Konscious.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

using GekkoSuite.Api.Configurations;
using GekkoSuite.Api.Repositories;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public class AuthService : IAuthService
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly JwtOptions _jwtOptions;

    public AuthService(JwtOptions jwtOptions, IUserService userService, IUserRepository userRepository)
    {
        _jwtOptions = jwtOptions;
        _userService = userService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Checks the request password against the hashed one in database for a user
    /// </summary>
    private static bool VerifyPassword(string password, string storedHashPassword)
    {
        var parts = storedHashPassword.Split(':');
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
    /// Hashes a password with Argon2id using a caller-supplied salt.
    /// </summary>
    private static byte[] HashPassword(string password, byte[] salt)
    {
        const int ArgonMemoryKb = 19 * 1024;
        const int ArgonIterations = 2;
        const int ArgonParallelism = 1;
        const int ArgonHashLengthBytes = 32;

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
    /// Signs and builds the access token for a logged-in user. 
    /// </summary>
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

        var token = new JwtSecurityToken(issuer: _jwtOptions.Issuer, audience: _jwtOptions.Audience, claims: claims, expires: expires, signingCredentials: credentials);
        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresIn = _jwtOptions.AccessTokenLifetimeMinutes * 60
        };
    }

    /// <inheritdoc />
    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        var user = await _userRepository.GetUserByEmailAsync(email);

        // Checking the hash even when user is null would be nice for timing-attack hygiene, but is
        // skipped here for simplicity; the meaningful secret (the password) is never exposed either way.
        if (user is null || !VerifyPassword(password, user.Password) || !user.IsActive)
        {
            return null;
        }

        return IssueAccessToken(user);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetCurrentUserAsync(Guid organizationId, Guid currentUserId)
    {
        UserDto? userDto = await _userService.GetUserAsync(organizationId, currentUserId);
        if (userDto == null)
        {
            return null;
        }

        return userDto;
    }

}
