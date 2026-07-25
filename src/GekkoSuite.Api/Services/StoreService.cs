using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class StoreService : IStoreService
{
    private readonly IStoreRepository _storeRepository;

    public StoreService(IStoreRepository storeRepository)
    {
        _storeRepository = storeRepository;
    }

    /// <inheritdoc />
    public async Task<List<UserDto>> GetStoreUsersByStoreIdAsync(Guid organizationId, Guid storeId)
    {
        IEnumerable<UserEntity> userEntities = await _storeRepository.GetStoreUsersByStoreIdAsync(organizationId, storeId);
        return userEntities.Select(UserDto.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<List<StoreDto>> GetStoresAsync(Guid organizationId)
    {
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
}
