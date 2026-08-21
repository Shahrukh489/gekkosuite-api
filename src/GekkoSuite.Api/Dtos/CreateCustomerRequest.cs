namespace GekkoSuite.Api.Dtos;

public class CreateCustomerRequest
{
    /// <summary>The customer's full name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Contact email; optional.</summary>
    public string? Email { get; set; }

    /// <summary>Contact phone; optional.</summary>
    public string? Phone { get; set; }
}
