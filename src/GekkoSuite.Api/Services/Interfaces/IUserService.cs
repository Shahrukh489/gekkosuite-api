using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public interface IUserService
{
    /// <summary>
    /// Finds a live user by id within the given organization, or null if not found there.
    /// </summary>
    Task<UserEntity?> GetUserByIdAsync(Guid organizationId, Guid userId);
}
