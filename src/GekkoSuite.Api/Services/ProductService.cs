using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    /// <inheritdoc />
    public async Task<List<ProductDto>> GetStoreProductsAsync(Guid organizationId, Guid storeId)
    {
        IEnumerable<ProductEntity> productEntities = await _productRepository.GetStoreProductsAsync(organizationId, storeId);
        return productEntities.Select(ProductDto.FromEntity).ToList();
    }
}
