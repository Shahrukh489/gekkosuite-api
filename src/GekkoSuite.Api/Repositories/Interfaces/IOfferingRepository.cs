using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Repositories;

public interface IOfferingRepository
{
    /// <summary>
    /// Gets the offerings, optionally filtered to one type (PLAN or ADDON).
    /// </summary>
    public Task<IEnumerable<OfferingEntity>> GetOfferingsAsync(OfferingType? type);
}
