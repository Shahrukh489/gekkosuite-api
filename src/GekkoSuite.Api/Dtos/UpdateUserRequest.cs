namespace GekkoSuite.Api.Dtos;

public class UpdateUserRequest
{
    /// <summary>New first name; unset leaves the current value.</summary>
    public string? FirstName { get; set; }

    /// <summary>New last name; unset leaves the current value.</summary>
    public string? LastName { get; set; }

    /// <summary>New login email; unset leaves the current value.</summary>
    public string? Email { get; set; }

    /// <summary>New account kill-switch state; unset leaves the current value.</summary>
    public bool? IsActive { get; set; }
}
