using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface ICustomerService
{
    /// <summary>
    /// Lists the customers at the given store — the store's own roster with contact details.
    /// </summary>
    Task<List<CustomerDto>> GetStoreCustomersAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Creates a customer at the given store. Throws BadRequestException if the name is blank,
    /// ConflictException if the email is already used at this store.
    /// </summary>
    Task<CustomerDto> CreateCustomerAsync(Guid organizationId, Guid storeId, CreateCustomerRequest request);
}
