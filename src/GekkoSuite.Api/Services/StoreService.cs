using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class StoreService : IStoreService
{
    private readonly IStoreRepository _storeRepository;
    private readonly IUserService _userService;

    public StoreService(IStoreRepository storeRepository, IUserService userService)
    {
        _storeRepository = storeRepository;
        _userService = userService;
    }

    /// <inheritdoc />
    public async Task<List<UserDto>> GetStoreUsersAsync(Guid organizationId, Guid storeId)
    {
        return await _userService.GetStoreUsersWithMembershipsAsync(organizationId, storeId);
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

}
