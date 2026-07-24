using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IOrganizationService
{
    /// <summary>
    /// Returns the organization record, or null if not found.
    /// </summary>
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId);
}
