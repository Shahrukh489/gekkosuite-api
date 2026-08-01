using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface IOrganizationRepository
{
    /// <summary>
    /// Finds the organization by id (with its live subscriptions), or null if not found (or soft-deleted).
    /// </summary>
    public Task<OrganizationEntity?> GetOrganizationByIdAsync(Guid organizationId);

    /// <summary>
    /// Lists the live stores in the given organization (the org's roster), default store first.
    /// </summary>
    public Task<IEnumerable<StoreEntity>> GetOrganizationStoresAsync(Guid organizationId);
}
