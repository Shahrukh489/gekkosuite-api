using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface IOrganizationRepository
{
    /// <summary>
    /// Finds the organization by id, or null if not found (or soft-deleted).
    /// </summary>
    public Task<OrganizationEntity?> GetOrganizationByIdAsync(Guid organizationId);
}
