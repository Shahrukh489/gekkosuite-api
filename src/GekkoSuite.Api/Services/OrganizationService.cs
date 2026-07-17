using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly OrganizationRepository _organizationRepository;

    public OrganizationService(OrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public string GetGreeting()
    {
        return "Hello from OrganizationService";
    }

    public Task<string> GetDatabaseVersionAsync(Guid organizationId)
    {
        return _organizationRepository.GetDatabaseVersionAsync(organizationId);
    }
}
