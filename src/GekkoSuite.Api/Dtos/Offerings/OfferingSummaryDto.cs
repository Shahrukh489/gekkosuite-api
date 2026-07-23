namespace GekkoSuite.Api.Dtos.Offerings;

/// <summary>
/// Response entry for GET /plans and GET /addons (docs/api.md) — a catalog offering, trimmed to what the
/// signup/add-ons picker needs.
/// </summary>
public class OfferingSummaryDto
{
    /// <summary>The offering's id.</summary>
    public Guid OfferingId { get; set; }

    /// <summary>Display name, e.g. "Pro", "Marketing".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional blurb about the offering.</summary>
    public string? Description { get; set; }

    /// <summary>Per-store price.</summary>
    public decimal PricePerStore { get; set; }
}
