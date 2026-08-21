using Npgsql;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public class OrderRepository : BaseRepository, IOrderRepository
{
    public OrderRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<IEnumerable<OrderEntity>> GetStoreOrdersAsync(Guid organizationId, Guid storeId)
    {
        const string sql = """
            SELECT
                o.order_id AS OrderId,
                o.store_id AS StoreId,
                o.organization_id AS OrganizationId,
                o.order_number AS OrderNumber,
                o.store_customer_id AS CustomerId,
                sc.name AS CustomerName,
                o.sold_by_user_id AS SoldByUserId,
                o.status::text AS Status,
                o.payment_method::text AS PaymentMethod,
                o.subtotal AS Subtotal,
                o.discount_total AS DiscountTotal,
                o.tax_total AS TaxTotal,
                o.total AS Total,
                o.created_at AS CreatedAt
            FROM sales_order o
            LEFT JOIN store_customer sc ON sc.store_customer_id = o.store_customer_id
            WHERE o.store_id = @storeId
              AND o.organization_id = @organizationId
            ORDER BY o.created_at DESC
            """;

        return QueryAsync<OrderEntity>(organizationId, storeId, sql, new { storeId, organizationId });
    }

    /// <inheritdoc />
    public Task<OrderEntity?> GetStoreOrderByIdAsync(Guid organizationId, Guid storeId, Guid orderId)
    {
        const string sql = """
            SELECT
                o.order_id AS OrderId,
                o.store_id AS StoreId,
                o.organization_id AS OrganizationId,
                o.order_number AS OrderNumber,
                o.store_customer_id AS CustomerId,
                sc.name AS CustomerName,
                o.sold_by_user_id AS SoldByUserId,
                o.status::text AS Status,
                o.payment_method::text AS PaymentMethod,
                o.subtotal AS Subtotal,
                o.discount_total AS DiscountTotal,
                o.tax_total AS TaxTotal,
                o.total AS Total,
                o.created_at AS CreatedAt,
                COALESCE(
                    (SELECT json_agg(
                                json_build_object(
                                    'productId', sp.store_product_id,
                                    'name', sp.name,
                                    'sku', sp.sku,
                                    'quantity', op.quantity,
                                    'unitPrice', op.unit_price
                                ) ORDER BY sp.name
                            )
                     FROM sales_order_product op
                     JOIN store_product sp ON sp.store_product_id = op.store_product_id
                     WHERE op.order_id = o.order_id),
                    '[]'
                ) AS Lines
            FROM sales_order o
            LEFT JOIN store_customer sc ON sc.store_customer_id = o.store_customer_id
            WHERE o.order_id = @orderId
              AND o.store_id = @storeId
              AND o.organization_id = @organizationId
            """;

        return QuerySingleOrDefaultAsync<OrderEntity>(organizationId, storeId, sql, new { orderId, storeId, organizationId });
    }

    /// <inheritdoc />
    public Task CreateOrderAsync(CreateOrderDto dto, DateTimeOffset now)
    {
        const string sql = """
            -- Serializes checkouts for this one store (held for the whole transaction, released at
            -- commit/rollback) so two concurrent sales at the same store can't both read the same
            -- "next order number" and collide. Checkouts at other stores are unaffected.
            SELECT pg_advisory_xact_lock(hashtext(@storeId::text));

            WITH next_number AS (
                -- Every seeded/existing order_number is a plain increasing integer string (the way a
                -- register prints receipt numbers), so this assumes that convention rather than
                -- enforcing it in the schema.
                SELECT COALESCE(MAX(order_number::integer), 1000) + 1 AS n
                FROM sales_order
                WHERE store_id = @storeId
            ),
            ins_order AS (
                INSERT INTO sales_order (
                    order_id, store_id, organization_id, order_number, store_customer_id, sold_by_user_id,
                    status, payment_method, subtotal, discount_total, tax_total, total, created_at
                )
                SELECT
                    @orderId, @storeId, @organizationId, next_number.n::text, @customerId, @soldByUserId,
                    'COMPLETED'::order_status, @paymentMethod::payment_method, @subtotal, @discountTotal, @taxTotal, @total, @now
                FROM next_number
            ),
            ins_lines AS (
                INSERT INTO sales_order_product (order_id, store_product_id, quantity, unit_price)
                SELECT @orderId, line.product_id, line.quantity, line.unit_price
                FROM unnest(@lineProductIds, @lineQuantities, @lineUnitPrices) AS line(product_id, quantity, unit_price)
            )
            -- Every tracked product sold loses the sold quantity from stock; a product with
            -- track_inventory off (e.g. a gift card) is left untouched. Negative stock is allowed by
            -- design (product.md) — overselling/backorder is real, not a bug, so this never blocks a sale.
            UPDATE store_product sp
            SET stock = sp.stock - line.quantity, updated_at = @now
            FROM unnest(@lineProductIds, @lineQuantities) AS line(product_id, quantity)
            WHERE sp.store_product_id = line.product_id
              AND sp.track_inventory
            """;

        return ExecuteAsync(dto.OrganizationId, dto.StoreId, sql, new
        {
            orderId = dto.OrderId,
            storeId = dto.StoreId,
            organizationId = dto.OrganizationId,
            customerId = dto.CustomerId,
            soldByUserId = dto.SoldByUserId,
            paymentMethod = dto.PaymentMethod.ToString(),
            subtotal = dto.Subtotal,
            discountTotal = dto.DiscountTotal,
            taxTotal = dto.TaxTotal,
            total = dto.Total,
            now,
            lineProductIds = dto.Lines.Select(line => line.ProductId).ToArray(),
            lineQuantities = dto.Lines.Select(line => line.Quantity).ToArray(),
            lineUnitPrices = dto.Lines.Select(line => line.UnitPrice).ToArray(),
        });
    }
}
