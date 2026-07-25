using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Entities;

public class StoreEntity
{
    /// <summary>The store's id.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The organization that owns the store (the tenant).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The store's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How the store sells (ONLINE or PHYSICAL).</summary>
    public StoreType Type { get; set; }

    /// <summary>The org's default store — where single-store orgs and org users land.</summary>
    public bool IsDefault { get; set; }

    /// <summary>When the store was created (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The store-scoped feature codes the org's live offerings enable; empty if none.</summary>
    public string[] Features { get; set; } = [];
}
