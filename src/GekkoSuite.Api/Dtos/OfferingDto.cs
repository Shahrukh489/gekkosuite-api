using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Dtos;

public class OfferingDto
{
    /// <summary>The offering's id.</summary>
    public Guid OfferingId { get; set; }

    /// <summary>The offering's type (PLAN or ADDON).</summary>
    public OfferingType Type { get; set; }

    /// <summary>The offering's display name (e.g. "Essentials").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description of what the offering includes.</summary>
    public string? Description { get; set; }

    /// <summary>The offering's per-store price.</summary>
    public decimal PricePerStore { get; set; }

    /// <summary>Map from OfferingEntity to OfferingDto.</summary>
    public static OfferingDto FromEntity(OfferingEntity offeringEntity)
    {
        return new OfferingDto()
        {
            OfferingId = offeringEntity.OfferingId,
            Type = offeringEntity.Type,
            Name = offeringEntity.Name,
            Description = offeringEntity.Description,
            PricePerStore = offeringEntity.PricePerStore,
        };
    }

    /// <summary>Map from an OfferingEntity list to an OfferingDto list.</summary>
    public static List<OfferingDto> FromEntityList(List<OfferingEntity> offeringEntities)
    {
        List<OfferingDto> offeringDtos = new List<OfferingDto>();

        for (int i = 0; i < offeringEntities.Count; i++)
        {
            offeringDtos.Add(FromEntity(offeringEntities[i]));
        }

        return offeringDtos;
    }
}
