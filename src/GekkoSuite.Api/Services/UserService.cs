using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Exceptions;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Services;


public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleService _roleService;
    private readonly IStoreRepository _storeRepository;

    // IStoreRepository, not IStoreService — StoreService itself depends on IUserService, so depending on
    // IStoreService here would be a circular dependency. The repository has no such dependency.
    public UserService(IUserRepository userRepository, IRoleService roleService, IStoreRepository storeRepository)
    {
        _userRepository = userRepository;
        _roleService = roleService;
        _storeRepository = storeRepository;
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByIdAsync(Guid organizationId, Guid userId)
    {
        UserEntity? userEntity = await _userRepository.GetUserByIdAsync(organizationId, userId);
        if (userEntity == null)
        {
            return null;
        }

        return UserDto.FromEntity(userEntity);
    }

    /// <inheritdoc />
    public async Task<Guid?> GetUserOrganizationIdAsync(Guid userId)
    {
        return await _userRepository.GetUserOrganizationIdAsync(userId);
    }

    /// <inheritdoc />
    public async Task<List<UserDto>> GetUsersWithMembershipsAsync(Guid organizationId)
    {
        IEnumerable<UserEntity> userEntities = await _userRepository.GetUsersWithMembershipsAsync(organizationId);
        return userEntities.Select(UserDto.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<List<UserDto>> GetUsersByStoreIdWithMembershipsAsync(Guid organizationId, Guid storeId)
    {
        IEnumerable<UserEntity> userEntities = await _userRepository.GetUsersByStoreIdWithMembershipsAsync(organizationId, storeId);
        return userEntities.Select(UserDto.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<List<MembershipDto>?> GetUserOrganizationMembershipsByOrganizationIdAsync(Guid organizationId, Guid userId)
    {
        IEnumerable<MembershipEntity> membershipEntities = await _userRepository.GetUserOrganizationMembershipsByOrganizationIdAsync(organizationId, userId);
        if (membershipEntities.Count() == 0)
        {
            return null;
        }

        // @TODO: add LRU in-memory-cache so we dont have to run that large query for every logged in user
        return MembershipDto.FromEntityList(membershipEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<List<MembershipDto>?> GetUserStoresMembershipsAsync(Guid organizationId, Guid userId)
    {
        IEnumerable<MembershipEntity> membershipEntities = await _userRepository.GetUserStoresMembershipsAsync(organizationId, userId);
        if (membershipEntities.Count() == 0)
        {
            return null;
        }

        // @TODO: add LRU in-memory-cache so we dont have to run that large query for every logged in user
        return MembershipDto.FromEntityList(membershipEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<List<MembershipDto>?> GetUserStoreMembershipsByStoreIdAsync(Guid organizationId, Guid userId, Guid storeId)
    {
        IEnumerable<MembershipEntity> membershipEntities = await _userRepository.GetUserStoreMembershipsByStoreIdAsync(organizationId, userId, storeId);
        if (membershipEntities.Count() == 0)
        {
            return null;
        }

        // @TODO: add LRU in-memory-cache so we dont have to run that large query for every logged in user
        return MembershipDto.FromEntityList(membershipEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByIdWithMembershipsAsync(Guid organizationId, Guid userId)
    {
        // @TODO: add LRU in-memory-cache so we dont have to run that large query for every logged in user
        UserDto? userDto = await GetUserByIdAsync(organizationId, userId);
        if (userDto is null)
        {
            return null;
        }

        // A user is EITHER an org member OR a store member, never both (the one-kind rule).
        // If we detect both then we throw an Exception, something went wrong in our database that allowed this to happen
        var orgMemberships = await GetUserOrganizationMembershipsByOrganizationIdAsync(organizationId, userId);
        var storeMemberships = await GetUserStoresMembershipsAsync(organizationId, userId);
        if (orgMemberships is not null && storeMemberships is not null)
        {
            throw new ValidationException($"User {userId} can not hold both an organization and store membership.");
        }

        // add the memberships a user
        if (orgMemberships is not null)
        {
            userDto.UserType = MembershipScope.ORGANIZATION;
            userDto.Memberships = orgMemberships;
        }
        else if (storeMemberships is not null)
        {
            userDto.UserType = MembershipScope.STORE;
            userDto.Memberships = storeMemberships;
            // dont send back the orgId
            userDto.OrganizationId = null;
        }
        else
        {
            // if no org or store memberships user type is none, he can not access anything
            userDto.UserType = null;
            userDto.Memberships = [];
        }
        return userDto;
    }

    /// <inheritdoc />
    public async Task<(UserDto User, string TemporaryPassword)> CreateUserAsync(CreateUserDto dto)
    {
        var firstName = dto.FirstName.Trim();
        var lastName = dto.LastName.Trim();
        var email = dto.Email.Trim().ToLowerInvariant();

        if (firstName.Length == 0 || lastName.Length == 0 || email.Length == 0)
        {
            throw new BadRequestException("First name, last name, and email are required.");
        }

        if (dto.RoleId == Guid.Empty)
        {
            throw new BadRequestException("A role is required to create a user.");
        }

        // Placing a STORE membership is still an org action — done from the org Users screen, picking a
        // place — not something a store user does for themselves (see api.md: "a store user manages
        // neither users nor access"). This endpoint stays ORGANIZATION-only unless the caller asks for STORE.
        if (dto.Scope == MembershipScope.STORE)
        {
            if (dto.StoreId is null || dto.StoreId == Guid.Empty)
            {
                throw new BadRequestException("A store is required to create a store user.");
            }

            if (await _storeRepository.GetStoreByIdAsync(dto.OrganizationId, dto.StoreId.Value) is null)
            {
                throw new BadRequestException("The selected store does not belong to this organization.");
            }
        }
        else if (dto.StoreId is not null)
        {
            throw new BadRequestException("A store can only be set when creating a store user.");
        }

        // The role must be one this org can assign (managed, or its own custom role) AND scoped to match
        // the membership being created — a STORE role on an org membership (or vice versa) would give the
        // wrong reach (see auth.md's scope-match rule).
        RoleDto? role = await _roleService.GetRoleByIdAsync(dto.OrganizationId, dto.RoleId, dto.Scope);
        if (role is null)
        {
            throw new BadRequestException($"The selected role is not a valid {dto.Scope} role for this organization.");
        }

        var userId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // No invite-email flow exists yet (auth.md's Security Review Notes), so a random temporary
        // password is generated here and returned once — it is never stored or logged in plaintext.
        var temporaryPassword = PasswordHasher.GenerateTemporaryPassword();
        var passwordHash = PasswordHasher.HashPassword(temporaryPassword);

        var normalizedDto = new CreateUserDto
        {
            OrganizationId = dto.OrganizationId,
            CreatedByUserId = dto.CreatedByUserId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Scope = dto.Scope,
            StoreId = dto.StoreId,
            RoleId = dto.RoleId,
        };

        await _userRepository.CreateUserAsync(normalizedDto, userId, passwordHash, membershipId, assignmentId, now);

        UserDto? createdUser = await GetUserByIdWithMembershipsAsync(dto.OrganizationId, userId);
        if (createdUser is null)
        {
            throw new InvalidOperationException($"User {userId} was created but could not be read back.");
        }

        return (createdUser, temporaryPassword);
    }

    /// <inheritdoc />
    public async Task<UserDto?> UpdateUserAsync(UpdateUserDto dto)
    {
        string? firstName = dto.FirstName?.Trim();
        string? lastName = dto.LastName?.Trim();
        string? email = dto.Email?.Trim().ToLowerInvariant();

        if (firstName?.Length == 0)
        {
            throw new BadRequestException("First name can not be blank.");
        }

        if (lastName?.Length == 0)
        {
            throw new BadRequestException("Last name can not be blank.");
        }

        if (email?.Length == 0)
        {
            throw new BadRequestException("Email can not be blank.");
        }

        // The owner's access can't be stripped this way — only a deliberate ownership transfer changes
        // it (see auth.md's "Who is the owner" FAQ and api.md's 409 on deactivate). Checked here, not
        // left to a WHERE-clause exclusion in the repository, so the caller gets a clear 409 instead of
        // a misleading 404.
        if (dto.IsActive == false)
        {
            UserEntity? existingUser = await _userRepository.GetUserByIdAsync(dto.OrganizationId, dto.UserId);
            if (existingUser is not null && existingUser.IsOrgOwner)
            {
                throw new ConflictException("The organization owner can not be deactivated.");
            }
        }

        var normalizedDto = new UpdateUserDto
        {
            OrganizationId = dto.OrganizationId,
            UserId = dto.UserId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            IsActive = dto.IsActive,
        };

        UserEntity? updated = await _userRepository.UpdateUserAsync(normalizedDto, DateTimeOffset.UtcNow);
        if (updated is null)
        {
            return null;
        }

        return await GetUserByIdWithMembershipsAsync(dto.OrganizationId, dto.UserId);
    }
}
