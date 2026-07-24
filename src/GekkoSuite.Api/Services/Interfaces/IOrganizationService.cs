namespace GekkoSuite.Api.Services;

public interface IOrganizationService
{
    /// <summary>
    /// Returns the flat, deduped set of permission codes the caller holds across their organization roles.
    /// </summary>
    Task<List<string>> GetUserOrganizationPermissionsAsync(Guid organizationId, Guid userId);
}
