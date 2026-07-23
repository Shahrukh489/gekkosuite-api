using GekkoSuite.Api.Models;

namespace GekkoSuite.Api.Repositories;

public interface IUserRepository
{
    /// <summary>
    /// Finds a live (not soft-deleted) user by their login email.
    /// </summary>
    /// <param name="email">The login email to look up.</param>
    /// <returns>The matching user, or null if no live account has that email.</returns>
    public Task<UserEntity?> FindByEmailAsync(string email);

    /// <summary>
    /// Finds a live (not soft-deleted) user by id within a specific organization. Used to re-check
    /// user.is_active fresh on every request once the JWT's claims are already validated — a token being
    /// unexpired doesn't mean the account wasn't deactivated a minute ago (see auth.md, §1).
    /// </summary>
    /// <param name="organizationId">The tenant the user must belong to (from the validated token).</param>
    /// <param name="userId">The user id to look up (from the validated token).</param>
    /// <returns>The matching user, or null if no live account matches both.</returns>
    public Task<UserEntity?> FindByIdAsync(Guid organizationId, Guid userId);
}
