using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationService(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    /// <inheritdoc />
    public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId)
    {
        OrganizationEntity? organizationEntity = await _organizationRepository.GetOrganizationByIdAsync(organizationId);
        if (organizationEntity == null)
        {
            return null;
        }

        return OrganizationDto.FromEntity(organizationEntity);
    }
}
