using System.Text.Json.Serialization;

using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Dtos;

public class StoreDto
{
    /// <summary>The store's id.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The organization that owns the store.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The store's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How the store sells (ONLINE or PHYSICAL).</summary>
    public StoreType Type { get; set; }

    /// <summary>The org's default store — where single-store orgs and org users land.</summary>
    public bool IsDefault { get; set; }

    /// <summary>When the store was created (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The store-scoped feature codes the org's live offerings enable; populated on the single-store view, omitted on the list.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Features { get; set; }

    /// <summary>Map from StoreEntity to StoreDto.</summary>
    public static StoreDto FromEntity(StoreEntity storeEntity)
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
