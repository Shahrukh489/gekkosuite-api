using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Services;

public interface IOfferingService
{
    /// <summary>
    /// Lists the active offerings, optionally filtered to one type (PLAN or ADDON).
    /// </summary>
    Task<List<OfferingDto>> GetOfferingsAsync(OfferingType? type);
}
