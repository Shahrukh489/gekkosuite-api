using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Services;

public interface IOfferingService
{
    /// <summary>
    /// Gets all the offerings
    /// </summary>
    Task<List<OfferingDto>> GetOfferingsAsync();
}
