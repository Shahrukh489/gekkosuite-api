using Npgsql;

namespace GekkoSuite.Api.Repositories;

public class OrganizationRepository : BaseRepository, IOrganizationRepository
{
    public OrganizationRepository(NpgsqlDataSource db) : base(db)
    {
    }

    public Task<string> GetDatabaseVersionAsync(Guid organizationId)
    {
        return QuerySingleAsync<string>(organizationId, "SELECT version();");
    }
}
