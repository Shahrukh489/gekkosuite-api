namespace GekkoSuite.Api.Dtos;

public class UpdateProductGroupDto
{
    /// <summary>The tenant the store belongs to (from the caller's token).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The store the group belongs to.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The group being updated.</summary>
    public Guid GroupId { get; set; }

    /// <summary>New display name; null leaves the current value.</summary>
    public string? Name { get; set; }

    /// <summary>New description; null leaves the current value.</summary>
    public string? Description { get; set; }

    /// <summary>New category; null leaves the current value.</summary>
    public string? Category { get; set; }

    /// <summary>New brand; null leaves the current value.</summary>
    public string? Brand { get; set; }
}
