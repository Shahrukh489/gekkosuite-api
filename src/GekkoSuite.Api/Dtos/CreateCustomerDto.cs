namespace GekkoSuite.Api.Dtos;

public class CreateCustomerDto
{
    /// <summary>The tenant the customer belongs to (from the caller's token).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The store this customer is being added at.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The customer's full name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Contact email; optional.</summary>
    public string? Email { get; set; }

    /// <summary>Contact phone; optional.</summary>
    public string? Phone { get; set; }
}
