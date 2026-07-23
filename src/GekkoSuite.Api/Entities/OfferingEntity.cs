namespace GekkoSuite.Api.Models;

/// <summary>
/// Maps a row of the "offering" table (docs/database.md) — a priced bundle of features an org can
/// subscribe to, either a baseline PLAN or a stackable ADDON (see docs/plans.md).
/// </summary>
public class OfferingEntity
{
    /// <summary>The offering's id.</summary>
    public Guid OfferingId { get; set; }

    /// <summary>"PLAN" (the baseline) or "ADDON" (a stackable extra). Kept as the raw Postgres enum
    /// label rather than a C# enum, since this project doesn't register a JsonStringEnumConverter and a
    /// real enum would otherwise serialize as a number.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Display name, e.g. "Basic", "Pro", "Marketing" (unique across offerings).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional blurb about the offering.</summary>
    public string? Description { get; set; }

    /// <summary>Per-store price; the bill adds this × store count for each of the org's live offerings.</summary>
    public decimal PricePerStore { get; set; }

    /// <summary>Still offered in the catalog? False = retired; existing subscriptions keep it.</summary>
    public bool IsActive { get; set; }
}
