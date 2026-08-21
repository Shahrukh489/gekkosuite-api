namespace GekkoSuite.Api.Dtos;

public class CreateOrderRequest
{
    /// <summary>The customer this sale is attached to; unset for a walk-in sale.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>How the sale was paid: CASH or CARD.</summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>The cart's lines. Only the product and quantity are taken from the client — price,
    /// tax, and totals are always computed server-side from the current store_product row, so a
    /// tampered request can't check out at a price the store never set.</summary>
    public List<CreateOrderLineRequest> Lines { get; set; } = [];
}

public class CreateOrderLineRequest
{
    /// <summary>The store_product being sold.</summary>
    public Guid ProductId { get; set; }

    /// <summary>How many units.</summary>
    public int Quantity { get; set; }
}
