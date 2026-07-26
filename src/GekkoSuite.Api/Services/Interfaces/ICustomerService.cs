using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface ICustomerService
{
    /// <summary>
    /// Lists the customers at the given store — the store's own roster with contact details.
    /// </summary>
    Task<List<CustomerDto>> GetStoreCustomersAsync(Guid organizationId, Guid storeId);
}
