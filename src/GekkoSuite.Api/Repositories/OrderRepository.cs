using Npgsql;

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
}
