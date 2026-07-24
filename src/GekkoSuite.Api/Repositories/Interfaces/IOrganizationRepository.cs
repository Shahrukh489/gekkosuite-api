using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface IOrganizationRepository
{
    /// <summary>
    /// Finds the organization by id, or null if not found (or soft-deleted).
    /// </summary>
    public Task<OrganizationEntity?> GetOrganizationByIdAsync(Guid organizationId);

    /// <summary>
    /// Returns the org's live subscriptions (one per subscription) — billing detail, no features.
    /// </summary>
    public Task<IEnumerable<SubscriptionEntity>> GetOrganizationSubscriptionsAsync(Guid organizationId);

    /// <summary>
    /// Returns the deduped ORGANIZATION-scoped feature codes across the org's live subscriptions' offerings.
    /// </summary>
    public Task<IEnumerable<string>> GetOrganizationFeaturesAsync(Guid organizationId);
}
