namespace GekkoSuite.Api.Services;

public interface IOrganizationService
{
    string GetGreeting();

    Task<string> GetDatabaseVersionAsync(Guid organizationId);
}
