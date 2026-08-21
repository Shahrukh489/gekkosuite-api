using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Exceptions;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;

    public OrderService(IOrderRepository orderRepository, IProductRepository productRepository, ICustomerRepository customerRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
    }

    /// <inheritdoc />
    public async Task<List<OrderDto>> GetStoreOrdersAsync(Guid organizationId, Guid storeId)
    {
        IEnumerable<OrderEntity> orderEntities = await _orderRepository.GetStoreOrdersAsync(organizationId, storeId);
        return orderEntities.Select(OrderDto.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<OrderDto?> GetStoreOrderByIdAsync(Guid organizationId, Guid storeId, Guid orderId)
    {
        OrderEntity? orderEntity = await _orderRepository.GetStoreOrderByIdAsync(organizationId, storeId, orderId);
        return orderEntity is null ? null : OrderDto.FromEntity(orderEntity);
    }

    /// <inheritdoc />
    public async Task<OrderDto> CreateOrderAsync(Guid organizationId, Guid storeId, Guid soldByUserId, CreateOrderRequest request)
    {
        if (request.Lines.Count == 0)
        {
            throw new BadRequestException("The cart is empty.");
        }

        if (request.Lines.Any(line => line.Quantity <= 0))
        {
            throw new BadRequestException("Every line must have a quantity of at least 1.");
        }

        if (!Enum.TryParse(request.PaymentMethod, out PaymentMethod paymentMethod))
        {
            throw new BadRequestException($"'{request.PaymentMethod}' is not a valid payment method.");
        }

        if (request.CustomerId is not null)
        {
            IEnumerable<CustomerEntity> storeCustomers = await _customerRepository.GetStoreCustomersAsync(organizationId, storeId);
            if (!storeCustomers.Any(customer => customer.CustomerId == request.CustomerId))
            {
                throw new BadRequestException("The selected customer is not at this store.");
            }
        }

        // Price, tax, and totals are never taken from the client — every line is repriced here from
        // the store's own current catalog, so a tampered cart can't check out below the real price.
        IEnumerable<ProductEntity> storeProducts = await _productRepository.GetStoreProductsAsync(organizationId, storeId);
        var productsById = storeProducts.ToDictionary(product => product.ProductId);

        var lines = new List<CreateOrderLineDto>();
        decimal subtotal = 0m;
        decimal taxTotal = 0m;

        foreach (CreateOrderLineRequest lineRequest in request.Lines)
        {
            if (!productsById.TryGetValue(lineRequest.ProductId, out ProductEntity? product) || !product.IsActive)
            {
                throw new BadRequestException($"Product {lineRequest.ProductId} is not sellable at this store.");
            }

            var lineSubtotal = product.Price * lineRequest.Quantity;
            subtotal += lineSubtotal;

            if (product.IsTaxable)
            {
                taxTotal += lineSubtotal * product.TaxRate / 100m;
            }

            lines.Add(new CreateOrderLineDto
            {
                ProductId = product.ProductId,
                Quantity = lineRequest.Quantity,
                UnitPrice = product.Price,
            });
        }

        taxTotal = Math.Round(taxTotal, 2, MidpointRounding.AwayFromZero);
        const decimal discountTotal = 0m;
        var total = subtotal - discountTotal + taxTotal;

        var orderId = Guid.NewGuid();
        var dto = new CreateOrderDto
        {
            OrderId = orderId,
            OrganizationId = organizationId,
            StoreId = storeId,
            SoldByUserId = soldByUserId,
            CustomerId = request.CustomerId,
            PaymentMethod = paymentMethod,
            Subtotal = subtotal,
            DiscountTotal = discountTotal,
            TaxTotal = taxTotal,
            Total = total,
            Lines = lines,
        };

        await _orderRepository.CreateOrderAsync(dto, DateTimeOffset.UtcNow);

        OrderDto? createdOrder = await GetStoreOrderByIdAsync(organizationId, storeId, orderId);
        if (createdOrder is null)
        {
            throw new InvalidOperationException($"Order {orderId} was created but could not be read back.");
        }

        return createdOrder;
    }

    /// <inheritdoc />
    public async Task<DashboardSummaryDto> GetStoreDashboardSummaryAsync(Guid organizationId, Guid storeId)
    {
        DashboardSummaryEntity summaryEntity = await _orderRepository.GetStoreDashboardSummaryAsync(organizationId, storeId);
        return DashboardSummaryDto.FromEntity(summaryEntity);
    }

    /// <inheritdoc />
    public async Task<OrganizationDashboardSummaryDto> GetOrganizationDashboardSummaryAsync(Guid organizationId)
    {
        OrganizationDashboardSummaryEntity summaryEntity = await _orderRepository.GetOrganizationDashboardSummaryAsync(organizationId);
        return OrganizationDashboardSummaryDto.FromEntity(summaryEntity);
    }
}
