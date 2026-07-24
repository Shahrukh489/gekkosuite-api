using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;


public interface IAuthRepository
{
    /// <summary>
    /// Finds a live user by their login email, returning the fields needed to authenticate (password hash,
    /// is_active). Used only by login, before the tenant is known — so it runs unscoped by RLS.
    /// </summary>
    public Task<UserEntity?> GetUserByEmailAsync(string email);
}
