using Npgsql;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Exceptions;

namespace GekkoSuite.Api.Repositories;

public class ProductRepository : BaseRepository, IProductRepository
{
    // Shared by every read that returns a ProductEntity: resolves Name/Description/Category/Brand from
    // the row itself when standalone, or from its group when grouped, and carries the group's own fields
    // (prefixed Group*) so ProductDto.FromEntity can build the nested group summary.
    private const string ResolvedProductColumns = """
        COALESCE(sp.name, spg.name) AS Name,
        COALESCE(sp.description, spg.description) AS Description,
        sp.sku AS Sku,
        sp.barcode AS Barcode,
        COALESCE(sp.category, spg.category) AS Category,
        COALESCE(sp.brand, spg.brand) AS Brand,
        sp.group_id AS GroupId,
        sp.variant_option_one_value AS VariantOptionOneValue,
        sp.variant_option_two_value AS VariantOptionTwoValue,
        sp.variant_option_three_value AS VariantOptionThreeValue,
        spg.name AS GroupName,
        spg.description AS GroupDescription,
        spg.category AS GroupCategory,
        spg.brand AS GroupBrand,
        spg.variant_option_one_name AS GroupVariantOptionOneName,
        spg.variant_option_two_name AS GroupVariantOptionTwoName,
        spg.variant_option_three_name AS GroupVariantOptionThreeName,
        sp.price AS Price,
        sp.cost AS Cost,
        sp.stock AS Stock,
        sp.is_taxable AS IsTaxable,
        sp.tax_rate AS TaxRate,
        sp.is_active AS IsActive,
        sp.created_at AS CreatedAt,
        sp.updated_at AS UpdatedAt
        """;

    public ProductRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<IEnumerable<ProductEntity>> GetStoreProductsAsync(Guid organizationId, Guid storeId)
    {
        string sql = $"""
            SELECT
                sp.store_product_id AS ProductId,
                sp.store_id AS StoreId,
                sp.organization_id AS OrganizationId,
                {ResolvedProductColumns}
            FROM store_product sp
            LEFT JOIN store_product_group spg ON sp.group_id = spg.group_id
            WHERE sp.store_id = @storeId
              AND sp.organization_id = @organizationId
              AND NOT sp.is_deleted
            ORDER BY COALESCE(sp.name, spg.name), sp.sku
            """;

        return QueryAsync<ProductEntity>(organizationId, storeId, sql, new { storeId, organizationId });
    }

    /// <inheritdoc />
    public async Task<ProductEntity> CreateProductAsync(CreateProductDto dto, Guid productId, DateTimeOffset now)
    {
        try
        {
            if (dto.NewGroup is not null)
            {
                return await CreateProductWithNewGroupAsync(dto, productId, now);
            }

            if (dto.GroupId is not null)
            {
                const string groupExistsSql = """
                    SELECT 1 FROM store_product_group
                    WHERE group_id = @groupId AND store_id = @storeId AND organization_id = @organizationId AND NOT is_deleted
                    """;

                int? found = await QuerySingleOrDefaultAsync<int?>(dto.OrganizationId, dto.StoreId, groupExistsSql, new
                {
                    groupId = dto.GroupId,
                    storeId = dto.StoreId,
                    organizationId = dto.OrganizationId,
                });

                if (found is null)
                {
                    throw new BadRequestException("Group not found.");
                }
            }

            // No dual-write to the shared `product` table: organization.allow_share_products doesn't exist
            // as a real column anywhere in the migrated schema (product.md documents it as a future toggle,
            // currently always "off"), so store_product is the only row a create can ever write today.
            // Covers both standalone (dto.GroupId null) and add-to-existing-group (dto.GroupId set) —
            // the LEFT JOIN resolves the group's fields either way, or nothing when there's no group.
            string sql = $"""
                WITH inserted AS (
                    INSERT INTO store_product (
                        store_product_id, store_id, organization_id, group_id, name, description, sku, barcode,
                        category, brand, variant_option_one_value, variant_option_two_value, variant_option_three_value,
                        price, cost, stock, is_taxable, tax_rate, is_active,
                        created_at, updated_at, is_deleted
                    )
                    VALUES (
                        @productId, @storeId, @organizationId, @groupId, @name, @description, @sku, @barcode,
                        @category, @brand, @variantOptionOneValue, @variantOptionTwoValue, @variantOptionThreeValue,
                        @price, @cost, @stock, @isTaxable, @taxRate, TRUE,
                        @now, @now, FALSE
                    )
                    RETURNING *
                )
                SELECT
                    inserted.store_product_id AS ProductId,
                    inserted.store_id AS StoreId,
                    inserted.organization_id AS OrganizationId,
                    {ResolvedProductColumns.Replace("sp.", "inserted.")}
                FROM inserted
                LEFT JOIN store_product_group spg ON inserted.group_id = spg.group_id
                """;

            return await QuerySingleAsync<ProductEntity>(dto.OrganizationId, dto.StoreId, sql, new
            {
                productId,
                storeId = dto.StoreId,
                organizationId = dto.OrganizationId,
                groupId = dto.GroupId,
                name = dto.Name,
                description = dto.Description,
                sku = dto.Sku,
                barcode = dto.Barcode,
                category = dto.Category,
                brand = dto.Brand,
                variantOptionOneValue = dto.VariantOptionOneValue,
                variantOptionTwoValue = dto.VariantOptionTwoValue,
                variantOptionThreeValue = dto.VariantOptionThreeValue,
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

    /// <summary>
    /// Creates a new store_product_group and its first variant together, in one statement — a group is
    /// never created empty.
    /// </summary>
    private async Task<ProductEntity> CreateProductWithNewGroupAsync(CreateProductDto dto, Guid productId, DateTimeOffset now)
    {
        Guid groupId = Guid.NewGuid();

        const string sql = """
            WITH new_group AS (
                INSERT INTO store_product_group (
                    group_id, store_id, organization_id, name, description, category, brand,
                    variant_option_one_name, variant_option_two_name, variant_option_three_name,
                    created_at, updated_at, is_deleted
                )
                VALUES (
                    @groupId, @storeId, @organizationId, @groupName, @groupDescription, @groupCategory, @groupBrand,
                    @groupVariantOptionOneName, @groupVariantOptionTwoName, @groupVariantOptionThreeName,
                    @now, @now, FALSE
                )
                RETURNING *
            ),
            inserted AS (
                INSERT INTO store_product (
                    store_product_id, store_id, organization_id, group_id, name, description, sku, barcode,
                    category, brand, variant_option_one_value, variant_option_two_value, variant_option_three_value,
                    price, cost, stock, is_taxable, tax_rate, is_active, created_at, updated_at, is_deleted
                )
                SELECT
                    @productId, @storeId, @organizationId, new_group.group_id, NULL, NULL, @sku, @barcode,
                    NULL, NULL, @variantOptionOneValue, @variantOptionTwoValue, @variantOptionThreeValue,
                    @price, @cost, @stock, @isTaxable, @taxRate, TRUE, @now, @now, FALSE
                FROM new_group
                RETURNING *
            )
            SELECT
                inserted.store_product_id AS ProductId,
                inserted.store_id AS StoreId,
                inserted.organization_id AS OrganizationId,
                COALESCE(inserted.name, new_group.name) AS Name,
                COALESCE(inserted.description, new_group.description) AS Description,
                inserted.sku AS Sku,
                inserted.barcode AS Barcode,
                COALESCE(inserted.category, new_group.category) AS Category,
                COALESCE(inserted.brand, new_group.brand) AS Brand,
                inserted.group_id AS GroupId,
                inserted.variant_option_one_value AS VariantOptionOneValue,
                inserted.variant_option_two_value AS VariantOptionTwoValue,
                inserted.variant_option_three_value AS VariantOptionThreeValue,
                new_group.name AS GroupName,
                new_group.description AS GroupDescription,
                new_group.category AS GroupCategory,
                new_group.brand AS GroupBrand,
                new_group.variant_option_one_name AS GroupVariantOptionOneName,
                new_group.variant_option_two_name AS GroupVariantOptionTwoName,
                new_group.variant_option_three_name AS GroupVariantOptionThreeName,
                inserted.price AS Price,
                inserted.cost AS Cost,
                inserted.stock AS Stock,
                inserted.is_taxable AS IsTaxable,
                inserted.tax_rate AS TaxRate,
                inserted.is_active AS IsActive,
                inserted.created_at AS CreatedAt,
                inserted.updated_at AS UpdatedAt
            FROM inserted, new_group
            """;

        return await QuerySingleAsync<ProductEntity>(dto.OrganizationId, dto.StoreId, sql, new
        {
            groupId,
            productId,
            storeId = dto.StoreId,
            organizationId = dto.OrganizationId,
            groupName = dto.NewGroup!.Name,
            groupDescription = dto.NewGroup.Description,
            groupCategory = dto.NewGroup.Category,
            groupBrand = dto.NewGroup.Brand,
            groupVariantOptionOneName = dto.NewGroup.VariantOptionOneName,
            groupVariantOptionTwoName = dto.NewGroup.VariantOptionTwoName,
            groupVariantOptionThreeName = dto.NewGroup.VariantOptionThreeName,
            sku = dto.Sku,
            barcode = dto.Barcode,
            variantOptionOneValue = dto.VariantOptionOneValue,
            variantOptionTwoValue = dto.VariantOptionTwoValue,
            variantOptionThreeValue = dto.VariantOptionThreeValue,
            price = dto.Price,
            cost = dto.Cost,
            stock = dto.Stock,
            isTaxable = dto.IsTaxable,
            taxRate = dto.TaxRate,
            now,
        });
    }

    /// <inheritdoc />
    public async Task<ProductEntity?> UpdateProductAsync(UpdateProductDto dto, DateTimeOffset now)
    {
        // name/description/category/brand are only settable on a standalone row — a grouped variant
        // defers those to its group (store_product_group_name_xor). This is the backstop; the service
        // is the primary check with a clear error.
        string sql = $"""
            WITH updated AS (
                UPDATE store_product
                SET name = CASE WHEN group_id IS NULL THEN COALESCE(@name, name) ELSE name END,
                    description = CASE WHEN group_id IS NULL THEN COALESCE(@description, description) ELSE description END,
                    sku = COALESCE(@sku, sku),
                    barcode = COALESCE(@barcode, barcode),
                    category = CASE WHEN group_id IS NULL THEN COALESCE(@category, category) ELSE category END,
                    brand = CASE WHEN group_id IS NULL THEN COALESCE(@brand, brand) ELSE brand END,
                    variant_option_one_value = COALESCE(@variantOptionOneValue, variant_option_one_value),
                    variant_option_two_value = COALESCE(@variantOptionTwoValue, variant_option_two_value),
                    variant_option_three_value = COALESCE(@variantOptionThreeValue, variant_option_three_value),
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
                RETURNING *
            )
            SELECT
                updated.store_product_id AS ProductId,
                updated.store_id AS StoreId,
                updated.organization_id AS OrganizationId,
                {ResolvedProductColumns.Replace("sp.", "updated.")}
            FROM updated
            LEFT JOIN store_product_group spg ON updated.group_id = spg.group_id
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
                variantOptionOneValue = dto.VariantOptionOneValue,
                variantOptionTwoValue = dto.VariantOptionTwoValue,
                variantOptionThreeValue = dto.VariantOptionThreeValue,
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

    /// <inheritdoc />
    public Task<StoreProductGroupEntity?> UpdateProductGroupAsync(UpdateProductGroupDto dto, DateTimeOffset now)
    {
        const string sql = """
            UPDATE store_product_group
            SET name = COALESCE(@name, name),
                description = COALESCE(@description, description),
                category = COALESCE(@category, category),
                brand = COALESCE(@brand, brand),
                updated_at = @now
            WHERE group_id = @groupId
              AND store_id = @storeId
              AND organization_id = @organizationId
              AND NOT is_deleted
            RETURNING
                group_id AS GroupId,
                store_id AS StoreId,
                organization_id AS OrganizationId,
                name AS Name,
                description AS Description,
                category AS Category,
                brand AS Brand,
                variant_option_one_name AS VariantOptionOneName,
                variant_option_two_name AS VariantOptionTwoName,
                variant_option_three_name AS VariantOptionThreeName,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        return QuerySingleOrDefaultAsync<StoreProductGroupEntity>(dto.OrganizationId, dto.StoreId, sql, new
        {
            dto.GroupId,
            dto.StoreId,
            dto.OrganizationId,
            name = dto.Name,
            description = dto.Description,
            category = dto.Category,
            brand = dto.Brand,
            now,
        });
    }
}
