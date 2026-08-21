using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Exceptions;
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

    /// <inheritdoc />
    public async Task<CustomerDto> CreateCustomerAsync(Guid organizationId, Guid storeId, CreateCustomerRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new BadRequestException("A name is required to add a customer.");
        }

        var dto = new CreateCustomerDto
        {
            OrganizationId = organizationId,
            StoreId = storeId,
            Name = name,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
        };

        CustomerEntity created = await _customerRepository.CreateCustomerAsync(dto, Guid.NewGuid(), DateTimeOffset.UtcNow);

        return CustomerDto.FromEntity(created);
    }
}
