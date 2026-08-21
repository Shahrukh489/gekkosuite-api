namespace GekkoSuite.Api.Dtos;

public class UpdateProductGroupRequest
{
    /// <summary>New display name; unset leaves the current value.</summary>
    public string? Name { get; set; }

    /// <summary>New description; unset leaves the current value.</summary>
    public string? Description { get; set; }

    /// <summary>New category; unset leaves the current value.</summary>
    public string? Category { get; set; }

    /// <summary>New brand; unset leaves the current value.</summary>
    public string? Brand { get; set; }
}
