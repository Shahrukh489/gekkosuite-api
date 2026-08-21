namespace GekkoSuite.Api.Dtos;

/// <summary>
/// The API-out shape for a just-created user: UserDto's client-safe fields plus the one-time
/// temporary password. There is no invite-email flow yet (docs/auth.md's Security Review Notes), so
/// this is the only moment the plaintext password exists outside the hash — it must be handed to the
/// new user out of band and is never returned again.
/// </summary>
public class CreateUserResponse : UserDto
{
    /// <summary>The generated temporary password, in plaintext, shown once.</summary>
    public string TemporaryPassword { get; set; } = string.Empty;

    /// <summary>Builds the response from the created user's safe Dto plus its one-time password.</summary>
    public static CreateUserResponse FromUserDto(UserDto user, string temporaryPassword)
    {
        return new CreateUserResponse
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            OrganizationId = user.OrganizationId,
            IsActive = user.IsActive,
            UserType = user.UserType,
            Memberships = user.Memberships,
            TemporaryPassword = temporaryPassword,
        };
    }
}
