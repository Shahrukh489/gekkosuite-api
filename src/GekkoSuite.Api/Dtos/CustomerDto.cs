using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class CustomerDto
{
    /// <summary>The store_customer's id.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>The store this customer belongs to.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The customer's full name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Contact email; null if unset.</summary>
    public string? Email { get; set; }

    /// <summary>Contact phone; null if unset.</summary>
    public string? Phone { get; set; }

    /// <summary>Opt-in flag for the store's loyalty/marketing list.</summary>
    public bool IsActive { get; set; }

    /// <summary>When the customer was first added at this store (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the customer was last modified (stored UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Map from CustomerEntity to CustomerDto.</summary>
    /// <param name="customerEntity">The store_customer row to map.</param>
    /// <returns>The client-safe customer DTO.</returns>
    public static CustomerDto FromEntity(CustomerEntity customerEntity)
    {
        return new CustomerDto()
        {
            CustomerId = customerEntity.CustomerId,
            StoreId = customerEntity.StoreId,
            Name = customerEntity.Name,
            Email = customerEntity.Email,
            Phone = customerEntity.Phone,
            IsActive = customerEntity.IsActive,
            CreatedAt = customerEntity.CreatedAt,
            UpdatedAt = customerEntity.UpdatedAt,
        };
    }
}
