namespace GekkoSuite.Api.Entities;

public class OrderProductEntity
{
    /// <summary>The store_product sold.</summary>
    public Guid ProductId { get; set; }

    /// <summary>The product's display name, snapshotted for the receipt.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The store's own product code; null if unset.</summary>
    public string? Sku { get; set; }

    /// <summary>How many units were sold on this line.</summary>
    public int Quantity { get; set; }

    /// <summary>Price per unit, snapshotted at sale time (not read live from store_product).</summary>
    public decimal UnitPrice { get; set; }
}
