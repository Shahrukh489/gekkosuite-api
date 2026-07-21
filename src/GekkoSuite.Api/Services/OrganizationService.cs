using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationService(IOrganizationRepository organizationRepository)
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
