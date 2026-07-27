using Npgsql;

using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Repositories;

public class OfferingRepository : BaseRepository, IOfferingRepository
{
    public OfferingRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<IEnumerable<OfferingEntity>> GetOfferingsAsync(OfferingType? type)
    {
        const string sql = """
            SELECT
                offering_id AS OfferingId,
                type::text AS Type,
                name AS Name,
                description AS Description,
                price_per_store AS PricePerStore
            FROM offering
            WHERE is_active
              AND (@type IS NULL OR type::text = @type)
            ORDER BY type, name
            """;

        return QueryUnscopedAsync<OfferingEntity>(sql, new { type = type?.ToString() });
    }
}
