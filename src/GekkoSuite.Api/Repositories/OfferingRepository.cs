using Npgsql;

using GekkoSuite.Api.Models;

namespace GekkoSuite.Api.Repositories;

public class OfferingRepository : BaseRepository, IOfferingRepository
{
    public OfferingRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <summary>
    /// Lists the active offerings of a given type ("PLAN" or "ADDON") from the catalog, ordered by name.
    /// </summary>
    /// <param name="type">The offering_type to filter by ("PLAN" or "ADDON").</param>
    /// <returns>The matching active offerings (empty if none).</returns>
    public Task<IEnumerable<OfferingEntity>> ListActiveByTypeAsync(string type)
    {
        // offering is a global catalog table (no organization_id column), so this runs unscoped —
        // there's no tenant for RLS to filter on here.
        const string sql = """
            SELECT
                offering_id AS OfferingId,
                type AS Type,
                name AS Name,
                description AS Description,
                price_per_store AS PricePerStore,
                is_active AS IsActive
            FROM offering
            WHERE type = @type::offering_type AND is_active
            ORDER BY name
            """;

        return QueryUnscopedAsync<OfferingEntity>(sql, new { type });
    }
}
