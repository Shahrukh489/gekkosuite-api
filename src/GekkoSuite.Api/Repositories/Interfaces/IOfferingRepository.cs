using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Repositories;

public interface IOfferingRepository
{
    /// <summary>
    /// Gets all the offerings
    /// </summary>
    public Task<IEnumerable<OfferingEntity>> GetOfferingsAsync();
}
