using Npgsql;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Exceptions;

namespace GekkoSuite.Api.Repositories;

public class ProductRepository : BaseRepository, IProductRepository
{
    public ProductRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<IEnumerable<ProductEntity>> GetStoreProductsAsync(Guid organizationId, Guid storeId)
    {
        const string sql = """
            SELECT
                store_product_id AS ProductId,
                store_id AS StoreId,
                organization_id AS OrganizationId,
                name AS Name,
                description AS Description,
                sku AS Sku,
                barcode AS Barcode,
                category AS Category,
                brand AS Brand,
                price AS Price,
                cost AS Cost,
                stock AS Stock,
                is_taxable AS IsTaxable,
                tax_rate AS TaxRate,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM store_product
            WHERE store_id = @storeId
              AND organization_id = @organizationId
              AND NOT is_deleted
            ORDER BY name
            """;

        return QueryAsync<ProductEntity>(organizationId, storeId, sql, new { storeId, organizationId });
    }

    /// <inheritdoc />
    public async Task<ProductEntity> CreateProductAsync(CreateProductDto dto, Guid productId, DateTimeOffset now)
    {
        // No dual-write to the shared `product` table: organization.allow_share_products doesn't exist
        // as a real column anywhere in the migrated schema (product.md documents it as a future toggle,
        // currently always "off"), so store_product is the only row a create can ever write today.
        const string sql = """
            INSERT INTO store_product (
                store_product_id, store_id, organization_id, name, description, sku, barcode,
                category, brand, price, cost, stock, is_taxable, tax_rate, is_active,
                created_at, updated_at, is_deleted
            )
            VALUES (
                @productId, @storeId, @organizationId, @name, @description, @sku, @barcode,
                @category, @brand, @price, @cost, @stock, @isTaxable, @taxRate, TRUE,
                @now, @now, FALSE
            )
            RETURNING
                store_product_id AS ProductId,
                store_id AS StoreId,
                organization_id AS OrganizationId,
                name AS Name,
                description AS Description,
                sku AS Sku,
                barcode AS Barcode,
                category AS Category,
                brand AS Brand,
                price AS Price,
                cost AS Cost,
                stock AS Stock,
                is_taxable AS IsTaxable,
                tax_rate AS TaxRate,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        try
        {
            return await QuerySingleAsync<ProductEntity>(dto.OrganizationId, dto.StoreId, sql, new
            {
                productId,
                storeId = dto.StoreId,
                organizationId = dto.OrganizationId,
                name = dto.Name,
                description = dto.Description,
                sku = dto.Sku,
                barcode = dto.Barcode,
                category = dto.Category,
                brand = dto.Brand,
                price = dto.Price,
                cost = dto.Cost,
                stock = dto.Stock,
                isTaxable = dto.IsTaxable,
                taxRate = dto.TaxRate,
                now,
            });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConflictException(ex.ConstraintName == "store_product_barcode_per_store"
                ? "A product with this barcode already exists at this store."
                : "A product with this SKU already exists at this store.");
        }
    }

    /// <inheritdoc />
    public async Task<ProductEntity?> UpdateProductAsync(UpdateProductDto dto, DateTimeOffset now)
    {
        const string sql = """
            UPDATE store_product
            SET name = COALESCE(@name, name),
                description = COALESCE(@description, description),
                sku = COALESCE(@sku, sku),
                barcode = COALESCE(@barcode, barcode),
                category = COALESCE(@category, category),
                brand = COALESCE(@brand, brand),
                price = COALESCE(@price, price),
                cost = COALESCE(@cost, cost),
                stock = COALESCE(@stock, stock),
                is_taxable = COALESCE(@isTaxable, is_taxable),
                tax_rate = COALESCE(@taxRate, tax_rate),
                is_active = COALESCE(@isActive, is_active),
                updated_at = @now
            WHERE store_product_id = @productId
              AND store_id = @storeId
              AND organization_id = @organizationId
              AND NOT is_deleted
            RETURNING
                store_product_id AS ProductId,
                store_id AS StoreId,
                organization_id AS OrganizationId,
                name AS Name,
                description AS Description,
                sku AS Sku,
                barcode AS Barcode,
                category AS Category,
                brand AS Brand,
                price AS Price,
                cost AS Cost,
                stock AS Stock,
                is_taxable AS IsTaxable,
                tax_rate AS TaxRate,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        try
        {
            return await QuerySingleOrDefaultAsync<ProductEntity>(dto.OrganizationId, dto.StoreId, sql, new
            {
                dto.ProductId,
                dto.StoreId,
                dto.OrganizationId,
                name = dto.Name,
                description = dto.Description,
                sku = dto.Sku,
                barcode = dto.Barcode,
                category = dto.Category,
                brand = dto.Brand,
                price = dto.Price,
                cost = dto.Cost,
                stock = dto.Stock,
                isTaxable = dto.IsTaxable,
                taxRate = dto.TaxRate,
                isActive = dto.IsActive,
                now,
            });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConflictException(ex.ConstraintName == "store_product_barcode_per_store"
                ? "A product with this barcode already exists at this store."
                : "A product with this SKU already exists at this store.");
        }
    }
}
