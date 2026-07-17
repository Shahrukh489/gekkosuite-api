namespace GekkoSuite.Api.Repositories;

public interface IOrganizationRepository
{
    public Task<string> GetDatabaseVersionAsync(Guid organizationId);
}
