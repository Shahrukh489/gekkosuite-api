using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Entities;

public class OfferingEntity
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
}
