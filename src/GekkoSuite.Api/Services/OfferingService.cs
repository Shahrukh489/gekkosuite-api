using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OfferingService : IOfferingService
{
    private readonly IOfferingRepository _offeringRepository;

    public OfferingService(IOfferingRepository offeringRepository)
    {
        _offeringRepository = offeringRepository;
    }

    /// <inheritdoc />
    public async Task<List<OfferingDto>> GetOfferingsAsync()
    {
        IEnumerable<OfferingEntity> offeringEntities = await _offeringRepository.GetOfferingsAsync();
        return OfferingDto.FromEntityList(offeringEntities.ToList());
    }
}
