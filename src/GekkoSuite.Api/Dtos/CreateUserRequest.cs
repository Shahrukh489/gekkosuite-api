namespace GekkoSuite.Api.Dtos;

public class CreateUserRequest
{
    /// <summary>The new user's first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>The new user's last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>The new user's login email; must be unique across the whole system.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The ORGANIZATION-scoped role to grant on the new organization membership.</summary>
    public Guid? RoleId { get; set; }
}
