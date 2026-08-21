using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Exceptions;
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

    /// <inheritdoc />
    public async Task<ProductDto> CreateProductAsync(Guid organizationId, Guid storeId, CreateProductRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new BadRequestException("A name is required to add a product.");
        }

        if (request.Price < 0)
        {
            throw new BadRequestException("Price can not be negative.");
        }

        var dto = new CreateProductDto
        {
            OrganizationId = organizationId,
            StoreId = storeId,
            Name = name,
            Description = NormalizeOptional(request.Description),
            Sku = NormalizeOptional(request.Sku),
            Barcode = NormalizeOptional(request.Barcode),
            Category = NormalizeOptional(request.Category),
            Brand = NormalizeOptional(request.Brand),
            Price = request.Price,
            Cost = request.Cost,
            Stock = request.Stock ?? 0,
            IsTaxable = request.IsTaxable ?? true,
            TaxRate = request.TaxRate ?? 0,
        };

        ProductEntity created = await _productRepository.CreateProductAsync(dto, Guid.NewGuid(), DateTimeOffset.UtcNow);

        return ProductDto.FromEntity(created);
    }

    /// <inheritdoc />
    public async Task<ProductDto?> UpdateProductAsync(Guid organizationId, Guid storeId, Guid productId, UpdateProductRequest request)
    {
        string? name = request.Name?.Trim();
        if (name?.Length == 0)
        {
            throw new BadRequestException("Name can not be blank.");
        }

        if (request.Price is < 0)
        {
            throw new BadRequestException("Price can not be negative.");
        }

        var dto = new UpdateProductDto
        {
            OrganizationId = organizationId,
            StoreId = storeId,
            ProductId = productId,
            Name = name,
            Description = request.Description,
            Sku = request.Sku,
            Barcode = request.Barcode,
            Category = request.Category,
            Brand = request.Brand,
            Price = request.Price,
            Cost = request.Cost,
            Stock = request.Stock,
            IsTaxable = request.IsTaxable,
            TaxRate = request.TaxRate,
            IsActive = request.IsActive,
        };

        ProductEntity? updated = await _productRepository.UpdateProductAsync(dto, DateTimeOffset.UtcNow);

        return updated is null ? null : ProductDto.FromEntity(updated);
    }

    /// <summary>
    /// Trims a string field and turns blank into null, so an empty form field clears rather than sets
    /// the value to whitespace.
    /// </summary>
    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length == 0 ? null : trimmed;
    }
}
