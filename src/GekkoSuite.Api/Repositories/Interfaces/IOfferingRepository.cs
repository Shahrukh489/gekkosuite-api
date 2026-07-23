using GekkoSuite.Api.Models;

namespace GekkoSuite.Api.Repositories;

public interface IOfferingRepository
{
    /// <summary>
    /// Lists the active offerings of a given type ("PLAN" or "ADDON") from the catalog, ordered by name.
    /// </summary>
    /// <param name="type">The offering_type to filter by ("PLAN" or "ADDON").</param>
    /// <returns>The matching active offerings (empty if none).</returns>
    public Task<IEnumerable<OfferingEntity>> ListActiveByTypeAsync(string type);
}
