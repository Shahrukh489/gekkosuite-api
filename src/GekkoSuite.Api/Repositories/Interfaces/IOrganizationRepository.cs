using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface IOrganizationRepository
{
    /// <summary>
    /// Get the organization details
    /// </summary>
    public Task<OrganizationEntity?> GetOrganizationByIdAsync(Guid organizationId);

    /// <summary>
    /// Get all the stores in the organization
    /// </summary>
    public Task<IEnumerable<StoreEntity>> GetOrganizationStoresAsync(Guid organizationId);

    /// <summary>
    /// Lists the live users in the org (metadata only, no memberships), ordered by first name.
    /// </summary>
    public Task<IEnumerable<UserEntity>> GetOrganizationUsersAsync(Guid organizationId);
}
