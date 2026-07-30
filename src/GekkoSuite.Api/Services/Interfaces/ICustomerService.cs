using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface ICustomerService
{
    /// <summary>
    /// Lists the customers at a given store
    /// </summary>
    Task<List<CustomerDto>> GetStoreCustomersAsync(Guid organizationId, Guid storeId);
}
