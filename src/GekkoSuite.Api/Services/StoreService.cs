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
    public async Task<StoreDto?> GetStoreByIdAsync(Guid organizationId, Guid storeId)
    {
        StoreEntity? storeEntity = await _storeRepository.GetStoreByIdAsync(organizationId, storeId);
        if (storeEntity == null)
        {
            return null;
        }

        return new StoreDto().FromEntity(storeEntity);
    }
}
