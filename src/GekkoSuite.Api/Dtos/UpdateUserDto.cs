namespace GekkoSuite.Api.Dtos;

public class UpdateUserDto
{
    /// <summary>The tenant the user must belong to (from the caller's token).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The user being updated.</summary>
    public Guid UserId { get; set; }

    /// <summary>New first name; null leaves the current value.</summary>
    public string? FirstName { get; set; }

    /// <summary>New last name; null leaves the current value.</summary>
    public string? LastName { get; set; }

    /// <summary>New login email (lowercased before this point); null leaves the current value.</summary>
    public string? Email { get; set; }

    /// <summary>New account kill-switch state; null leaves the current value.</summary>
    public bool? IsActive { get; set; }
}
