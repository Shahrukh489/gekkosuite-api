using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IStoreService
{
    /// <summary>
    /// Lists the users with a store membership at the given store (the staff roster), each with their membership(s).
    /// </summary>
    Task<List<UserDto>> GetStoreUsersByStoreIdAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Lists the stores in the given organization (the org's roster), default store first.
    /// </summary>
    Task<List<StoreDto>> GetStoresAsync(Guid organizationId);

    /// <summary>
    /// Finds a store by id within the given organization (with its store-scoped features), or null if it isn't in that org.
    /// </summary>
    Task<StoreDto?> GetStoreByIdAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Lists the STORE-scoped roles assignable in the org (managed + the org's own), for a store the caller belongs to.
    /// </summary>
    Task<List<RoleDto>> GetStoreRolesAsync(Guid organizationId);

    /// <summary>
    /// Returns one STORE-scoped role and the permissions it grants, or null if not visible to the org or not a store role.
    /// </summary>
    Task<RoleDto?> GetStoreRoleByIdAsync(Guid organizationId, Guid roleId);
}
