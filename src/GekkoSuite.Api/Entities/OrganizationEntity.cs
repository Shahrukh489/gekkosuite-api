using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Entities;

public class OrganizationEntity
{
    /// <summary>The organization's id (the tenant).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The business's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional note about the business.</summary>
    public string? Description { get; set; }

    /// <summary>The org's overall billing status.</summary>
    public BillingStatus BillingStatus { get; set; }

    /// <summary>When the org was created (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
