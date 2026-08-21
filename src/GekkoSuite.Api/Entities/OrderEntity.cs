using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Entities;

public class OrderEntity
{
    /// <summary>The order's id.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The store the sale belongs to.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The organization that owns the store (the tenant).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Human-readable receipt number shown to the customer/cashier.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>The customer, if attached; null for a walk-in / anonymous sale.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>The customer's name, if attached; null for a walk-in / anonymous sale.</summary>
    public string? CustomerName { get; set; }

    /// <summary>The user (cashier) who rang the sale; null if unknown.</summary>
    public Guid? SoldByUserId { get; set; }

    /// <summary>OPEN | COMPLETED | VOIDED.</summary>
    public OrderStatus Status { get; set; }

    /// <summary>How the sale was paid.</summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>Sum of line (unit price × quantity).</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Order-level discount applied.</summary>
    public decimal DiscountTotal { get; set; }

    /// <summary>Tax charged.</summary>
    public decimal TaxTotal { get; set; }

    /// <summary>What the customer paid (subtotal - discount total + tax total).</summary>
    public decimal Total { get; set; }

    /// <summary>When the sale was made (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The order's line items (folded from json_agg); only populated on a single-order read.</summary>
    public List<OrderProductEntity> Lines { get; set; } = [];
}
