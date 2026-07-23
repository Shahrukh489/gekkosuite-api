using GekkoSuite.Api.Dtos.Offerings;

namespace GekkoSuite.Api.Services;

public interface IOfferingService
{
    /// <summary>
    /// Lists the available base plans (offering.type = 'PLAN') for the signup and change-plan screens.
    /// </summary>
    /// <returns>The active plan offerings.</returns>
    Task<IEnumerable<OfferingSummaryDto>> GetPlansAsync();

    /// <summary>
    /// Lists the available add-ons (offering.type = 'ADDON') for the in-app add-ons screen.
    /// </summary>
    /// <returns>The active add-on offerings.</returns>
    Task<IEnumerable<OfferingSummaryDto>> GetAddonsAsync();
}
