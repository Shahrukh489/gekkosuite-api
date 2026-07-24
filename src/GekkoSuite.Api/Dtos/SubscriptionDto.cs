using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Dtos;

public class SubscriptionDto
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

    /// <summary>Map from SubscriptionEntity to SubscriptionDto.</summary>
    public SubscriptionDto FromEntity(SubscriptionEntity subscriptionEntity)
    {
        return new SubscriptionDto()
        {
            SubscriptionId = subscriptionEntity.SubscriptionId,
            OfferingId = subscriptionEntity.OfferingId,
            OfferingName = subscriptionEntity.OfferingName,
            OfferingType = subscriptionEntity.OfferingType,
            Status = subscriptionEntity.Status,
            PricePerStore = subscriptionEntity.PricePerStore,
            TrialEndsAt = subscriptionEntity.TrialEndsAt,
            CurrentPeriodEnd = subscriptionEntity.CurrentPeriodEnd,
        };
    }

    /// <summary>Map from a list of SubscriptionEntity to a list of SubscriptionDto.</summary>
    public List<SubscriptionDto> FromEntityList(List<SubscriptionEntity> subscriptionEntities)
    {
        List<SubscriptionDto> subscriptionDtos = new List<SubscriptionDto>();

        for (int i = 0; i < subscriptionEntities.Count; i++)
        {
            subscriptionDtos.Add(new SubscriptionDto().FromEntity(subscriptionEntities[i]));
        }

        return subscriptionDtos;
    }
}
