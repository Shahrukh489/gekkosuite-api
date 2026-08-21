using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IStoreService _storeService;
    private readonly IOrderService _orderService;

    public OrganizationService(IOrganizationRepository organizationRepository, IUserService userService, IRoleService roleService, IStoreService storeService, IOrderService orderService)
    {
        _organizationRepository = organizationRepository;
        _userService = userService;
        _roleService = roleService;
        _storeService = storeService;
        _orderService = orderService;
    }

    /// <inheritdoc />
    public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId)
    {
        OrganizationEntity? organizationEntity = await _organizationRepository.GetOrganizationByIdAsync(organizationId);
        if (organizationEntity == null)
        {
            return null;
        }

        return OrganizationDto.FromEntity(organizationEntity);
    }

    /// <inheritdoc />
    public async Task<List<UserDto>> GetUsersAsync(Guid organizationId)
    {
        return await _userService.GetUsersWithMembershipsAsync(organizationId);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByIdAsync(Guid organizationId, Guid userId)
    {
        return await _userService.GetUserByIdWithMembershipsAsync(organizationId, userId);
    }

    /// <inheritdoc />
    public async Task<CreateUserResponse> CreateUserAsync(Guid organizationId, Guid createdByUserId, CreateUserRequest request)
    {
        var dto = new CreateUserDto
        {
            OrganizationId = organizationId,
            CreatedByUserId = createdByUserId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Scope = request.Scope ?? MembershipScope.ORGANIZATION,
            StoreId = request.StoreId,
            RoleId = request.RoleId ?? Guid.Empty,
        };

        (UserDto user, string temporaryPassword) = await _userService.CreateUserAsync(dto);

        return CreateUserResponse.FromUserDto(user, temporaryPassword);
    }

    /// <inheritdoc />
    public async Task<UserDto?> UpdateUserAsync(Guid organizationId, Guid userId, UpdateUserRequest request)
    {
        var dto = new UpdateUserDto
        {
            OrganizationId = organizationId,
            UserId = userId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            IsActive = request.IsActive,
        };

        return await _userService.UpdateUserAsync(dto);
    }

    /// <inheritdoc />
    public async Task<List<RoleDto>> GetRolesAsync(Guid organizationId, MembershipScope? scope)
    {
        return await _roleService.GetRolesAsync(organizationId, scope);
    }

    /// <inheritdoc />
    public async Task<RoleDto?> GetRoleByIdAsync(Guid organizationId, Guid roleId)
    {
        return await _roleService.GetRoleByIdAsync(organizationId, roleId);
    }

    /// <inheritdoc />
    public async Task<List<StoreDto>> GetStoresAsync(Guid organizationId)
    {
        return await _storeService.GetStoresAsync(organizationId);
    }

    /// <inheritdoc />
    public async Task<OrganizationDashboardSummaryDto> GetDashboardSummaryAsync(Guid organizationId)
    {
        return await _orderService.GetOrganizationDashboardSummaryAsync(organizationId);
    }
}
