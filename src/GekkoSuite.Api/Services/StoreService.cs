using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class StoreService : IStoreService
{
    private readonly IStoreRepository _storeRepository;
    private readonly IProductService _productService;
    private readonly ICustomerService _customerService;
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;

    public StoreService(IStoreRepository storeRepository, IProductService productService, ICustomerService customerService, IUserService userService, IRoleService roleService)
    {
        _storeRepository = storeRepository;
        _productService = productService;
        _customerService = customerService;
        _userService = userService;
        _roleService = roleService;
    }

    /// <inheritdoc />
    public async Task<List<UserDto>> GetStoreUsersByStoreIdAsync(Guid organizationId, Guid storeId)
    {
        return await _userService.GetStoreUsersWithMembershipsAsync(organizationId, storeId);
    }

    /// <inheritdoc />
    public async Task<List<StoreDto>> GetStoresAsync(Guid organizationId)
    {
        // @TODO: verify if this will return null or empty list
        IEnumerable<StoreEntity> storeEntities = await _storeRepository.GetStoresAsync(organizationId);
        return storeEntities.Select(StoreDto.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<StoreDto?> GetStoreByIdAsync(Guid organizationId, Guid storeId)
    {
        StoreEntity? storeEntity = await _storeRepository.GetStoreByIdAsync(organizationId, storeId);
        if (storeEntity == null)
        {
            return null;
        }

        return StoreDto.FromEntity(storeEntity);
    }

    /// <inheritdoc />
    public async Task<List<RoleDto>> GetStoreRolesAsync(Guid organizationId)
    {
        return await _roleService.GetRolesAsync(organizationId, MembershipScope.STORE);
    }

    /// <inheritdoc />
    public async Task<RoleDto?> GetStoreRoleByIdAsync(Guid organizationId, Guid roleId)
    {
        return await _roleService.GetRoleByIdAsync(organizationId, roleId, MembershipScope.STORE);
    }

    /// <inheritdoc />
    public async Task<List<ProductDto>> GetStoreProductsAsync(Guid organizationId, Guid storeId)
    {
        return await _productService.GetStoreProductsAsync(organizationId, storeId);
    }

    /// <inheritdoc />
    public async Task<List<CustomerDto>> GetStoreCustomersAsync(Guid organizationId, Guid storeId)
    {
        return await _customerService.GetStoreCustomersAsync(organizationId, storeId);
    }
}
