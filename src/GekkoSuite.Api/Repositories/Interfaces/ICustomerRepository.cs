using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface ICustomerRepository
{
    /// <summary>
    /// Lists the live customers at the given store (the store's own roster), by name.
    /// </summary>
    public Task<IEnumerable<CustomerEntity>> GetStoreCustomersAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Creates a customer at the given store and returns the created row.
    /// </summary>
    public Task<CustomerEntity> CreateCustomerAsync(CreateCustomerDto dto, Guid customerId, DateTimeOffset now);
}
