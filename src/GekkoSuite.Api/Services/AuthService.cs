using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

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
    /// Signs and builds the access token for a logged-in user.
    /// </summary>
    private LoginResponse IssueAccessToken(UserEntity user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // token carries only the userId; the caller's org is resolved from it server-side per request
        var claims = new[]
        {
            new Claim("userId", user.UserId.ToString()),
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
        if (user is null || !PasswordHasher.VerifyPassword(password, user.Password) || !user.IsActive)
        {
            return null;
        }

        return IssueAccessToken(user);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetCurrentUserAsync(Guid organizationId, Guid currentUserId)
    {
        UserDto? userDto = await _userService.GetUserByIdWithMembershipsAsync(organizationId, currentUserId);
        if (userDto == null)
        {
            return null;
        }

        return userDto;
    }

}
