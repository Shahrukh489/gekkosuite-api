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

        return new OrganizationDto().FromEntity(organizationEntity);
    }

    /// <inheritdoc />
    public async Task<List<SubscriptionDto>?> GetOrganizationSubscriptionsAsync(Guid organizationId)
    {
        IEnumerable<SubscriptionEntity> subscriptionEntities = await _organizationRepository.GetOrganizationSubscriptionsAsync(organizationId);
        if (subscriptionEntities.Count() == 0)
        {
            return null;
        }

        return new SubscriptionDto().FromEntityList(subscriptionEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<List<string>> GetOrganizationFeaturesAsync(Guid organizationId)
    {
        IEnumerable<string> features = await _organizationRepository.GetOrganizationFeaturesAsync(organizationId);
        return features.ToList();
    }

    /// <inheritdoc />
    public async Task<OrganizationDto?> GetOrganizationAsync(Guid organizationId)
    {
        OrganizationDto? organizationDto = await GetOrganizationByIdAsync(organizationId);
        if (organizationDto is null)
        {
            return null;
        }

        List<SubscriptionDto>? subscriptions = await GetOrganizationSubscriptionsAsync(organizationId);
        if (subscriptions is not null)
        {
            organizationDto.Subscriptions = subscriptions;
        }

        organizationDto.Features = await GetOrganizationFeaturesAsync(organizationId);

        return organizationDto;
    }
}
