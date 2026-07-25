using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Repositories;

public interface IOfferingRepository
{
    /// <summary>
    /// Lists the active offerings, optionally filtered to one type (PLAN or ADDON). Global catalog, no tenant.
    /// </summary>
    public Task<IEnumerable<OfferingEntity>> GetOfferingsAsync(OfferingType? type);
}
