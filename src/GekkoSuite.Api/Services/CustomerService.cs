using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    /// <inheritdoc />
    public async Task<List<CustomerDto>> GetStoreCustomersAsync(Guid organizationId, Guid storeId)
    {
        IEnumerable<CustomerEntity> customerEntities = await _customerRepository.GetStoreCustomersAsync(organizationId, storeId);
        return customerEntities.Select(CustomerDto.FromEntity).ToList();
    }
}
