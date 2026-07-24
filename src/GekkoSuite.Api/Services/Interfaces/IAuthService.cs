using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IAuthService
{
    /// <summary>
    /// Verifies an email + password against the stored credentials
    /// </summary>
    Task<LoginResponse?> LoginAsync(string email, string password);
}
