using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Dtos;

public class OrganizationDto
{
    /// <summary>The organization's id.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The business's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional note about the business.</summary>
    public string? Description { get; set; }

    /// <summary>The org's overall billing status.</summary>
    public BillingStatus BillingStatus { get; set; }

    /// <summary>When the org was created (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The org's live subscriptions (billing detail); empty if it has none.</summary>
    public List<SubscriptionDto> Subscriptions { get; set; } = [];

    /// <summary>The org-scoped feature codes the org's live offerings enable (for gating org screens).</summary>
    public List<string> Features { get; set; } = [];

    /// <summary>Map from OrganizationEntity to OrganizationDto.</summary>
    public OrganizationDto FromEntity(OrganizationEntity organizationEntity)
    {
        return new OrganizationDto()
        {
            OrganizationId = organizationEntity.OrganizationId,
            Name = organizationEntity.Name,
            Description = organizationEntity.Description,
            BillingStatus = organizationEntity.BillingStatus,
            CreatedAt = organizationEntity.CreatedAt,
        };
    }
}
