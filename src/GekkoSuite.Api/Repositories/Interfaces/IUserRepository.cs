using GekkoSuite.Api.Models;

namespace GekkoSuite.Api.Repositories;

public interface IUserRepository
{
    /// <summary>
    /// Finds a live (not soft-deleted) user by their login email.
    /// </summary>
    /// <param name="email">The login email to look up.</param>
    /// <returns>The matching user, or null if no live account has that email.</returns>
    public Task<User?> FindByEmailAsync(string email);
}
