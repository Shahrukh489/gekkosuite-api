using GekkoSuite.Api.Dtos.Offerings;
using GekkoSuite.Api.Models;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OfferingService : IOfferingService
{
    private readonly IOfferingRepository _offeringRepository;

    public OfferingService(IOfferingRepository offeringRepository)
    {
        _offeringRepository = offeringRepository;
    }

    /// <summary>
    /// Lists the available base plans (offering.type = 'PLAN') for the signup and change-plan screens.
    /// </summary>
    /// <returns>The active plan offerings.</returns>
    public async Task<IEnumerable<OfferingSummaryDto>> GetPlansAsync()
    {
        var offerings = await _offeringRepository.ListActiveByTypeAsync("PLAN");
        return offerings.Select(ToSummaryDto);
    }

    /// <summary>
    /// Lists the available add-ons (offering.type = 'ADDON') for the in-app add-ons screen.
    /// </summary>
    /// <returns>The active add-on offerings.</returns>
    public async Task<IEnumerable<OfferingSummaryDto>> GetAddonsAsync()
    {
        var offerings = await _offeringRepository.ListActiveByTypeAsync("ADDON");
        return offerings.Select(ToSummaryDto);
    }

    /// <summary>
    /// Maps an offering row to the trimmed shape GET /plans and GET /addons return.
    /// </summary>
    /// <param name="offering">The offering entity read from the repository.</param>
    /// <returns>The response DTO.</returns>
    private static OfferingSummaryDto ToSummaryDto(OfferingEntity offering)
    {
        return new OfferingSummaryDto
        {
            OfferingId = offering.OfferingId,
            Name = offering.Name,
            Description = offering.Description,
            PricePerStore = offering.PricePerStore
        };
    }
}
