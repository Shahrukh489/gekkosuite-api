using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class StoreDto
{
    /// <summary>The store's id.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The organization that owns the store.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The store's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How the store sells: "ONLINE" or "PHYSICAL".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The org's default store — where single-store orgs and org users land.</summary>
    public bool IsDefault { get; set; }

    /// <summary>When the store was created (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Map from StoreEntity to StoreDto.</summary>
    public StoreDto FromEntity(StoreEntity storeEntity)
    {
        return new StoreDto()
        {
            StoreId = storeEntity.StoreId,
            OrganizationId = storeEntity.OrganizationId,
            Name = storeEntity.Name,
            Type = storeEntity.Type,
            IsDefault = storeEntity.IsDefault,
            CreatedAt = storeEntity.CreatedAt,
        };
    }
}
