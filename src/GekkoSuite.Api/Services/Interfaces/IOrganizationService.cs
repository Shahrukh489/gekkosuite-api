using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IOrganizationService
{
    /// <summary>
    /// Returns the organization record, or null if not found.
    /// </summary>
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId);

    /// <summary>
    /// Returns the org's live subscriptions (billing detail), or null if it has none.
    /// </summary>
    Task<List<SubscriptionDto>?> GetOrganizationSubscriptionsAsync(Guid organizationId);

    /// <summary>
    /// Returns the org's deduped ORGANIZATION-scoped feature codes across its live subscriptions.
    /// </summary>
    Task<List<string>> GetOrganizationFeaturesAsync(Guid organizationId);

    /// <summary>
    /// Builds the full organization view — the record plus its live subscriptions. Null if not found.
    /// </summary>
    Task<OrganizationDto?> GetOrganizationAsync(Guid organizationId);
}
