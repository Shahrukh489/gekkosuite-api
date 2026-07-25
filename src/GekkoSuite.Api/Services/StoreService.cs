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

    /// <inheritdoc />
    public async Task<List<string>> GetStoreFeaturesAsync(Guid organizationId)
    {
        IEnumerable<string> features = await _storeRepository.GetStoreFeaturesAsync(organizationId);
        return features.ToList();
    }

    /// <inheritdoc />
    public async Task<StoreDto?> GetStoreAsync(Guid organizationId, Guid storeId)
    {
        StoreDto? storeDto = await GetStoreByIdAsync(organizationId, storeId);
        if (storeDto is null)
        {
            return null;
        }

        storeDto.Features = await GetStoreFeaturesAsync(organizationId);

        return storeDto;
    }
}
