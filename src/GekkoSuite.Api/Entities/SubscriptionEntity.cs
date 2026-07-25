using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Entities;

public class SubscriptionEntity
{
    /// <summary>The subscription's id.</summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>The offering this subscription is for.</summary>
    public Guid OfferingId { get; set; }

    /// <summary>The offering's display name (e.g. "Essentials").</summary>
    public string OfferingName { get; set; } = string.Empty;

    /// <summary>The offering's type (PLAN or ADDON).</summary>
    public OfferingType OfferingType { get; set; }

    /// <summary>The subscription's lifecycle status (TRIALING, ACTIVE, or CANCELED).</summary>
    public OfferingStatus Status { get; set; }

    /// <summary>The offering's per-store price.</summary>
    public decimal PricePerStore { get; set; }

    /// <summary>When a free trial ends; null if not trialing.</summary>
    public DateTimeOffset? TrialEndsAt { get; set; }

    /// <summary>End of the current paid period (renewal boundary); null if not set.</summary>
    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    /// <summary>The offering's feature codes</summary>
    public string[] Features { get; set; } = [];
}
