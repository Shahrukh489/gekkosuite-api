using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;

    public OrderService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
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
}
