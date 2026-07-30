using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Exceptions;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Services;


public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
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
    public async Task<List<UserDto>> GetStoreUsersWithMembershipsAsync(Guid organizationId, Guid storeId)
    {
        IEnumerable<UserEntity> userEntities = await _userRepository.GetStoreUsersWithMembershipsAsync(organizationId, storeId);
        return userEntities.Select(UserDto.FromEntity).ToList();
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
    public async Task<List<MembershipDto>?> GetUserStoreMembershipsAsync(Guid organizationId, Guid userId, Guid storeId)
    {
        IEnumerable<MembershipEntity> membershipEntities = await _userRepository.GetUserStoreMembershipsAsync(organizationId, userId, storeId);
        if (membershipEntities.Count() == 0)
        {
            return null;
        }

        // @TODO: add LRU in-memory-cache so we dont have to run that large query for every logged in user

        // @ TODO: use one of these patterns not both userEntities.Select(UserDto.FromEntity).ToList();
        return MembershipDto.FromEntityList(membershipEntities.ToList());
    }


    /// <inheritdoc />
    public async Task<List<UserDto>> GetOrganizationUsersWithMembershipsAsync(Guid organizationId)
    {
        IEnumerable<UserEntity> userEntities = await _userRepository.GetOrganizationUsersWithMembershipsAsync(organizationId);
        return userEntities.Select(UserDto.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<List<MembershipDto>?> GetUserOrganizationMembershipsAsync(Guid organizationId, Guid userId)
    {
        // @TODO: add LRU in-memory-cache so we dont have to run that large query for every logged in user
        // @TODO: verify if this has no records it returns null or an empty list
        IEnumerable<MembershipEntity> membershipEntities = await _userRepository.GetUserOrganizationMembershipsAsync(organizationId, userId);
        if (membershipEntities.Count() == 0)
        {
            return null;
        }

        return MembershipDto.FromEntityList(membershipEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserWithMembershipsAsync(Guid organizationId, Guid userId)
    {
        // @TODO: add LRU in-memory-cache so we dont have to run that large query for every logged in user
        UserDto? userDto = await GetUserByIdAsync(organizationId, userId);
        if (userDto is null)
        {
            return null;
        }

        // A user is EITHER an org member OR a store member, never both.
        // If we detect both then we throw an Exception,
        // something went wrong in our database that allowed this to happen
        var orgMemberships = await GetUserOrganizationMembershipsAsync(organizationId, userId);
        var storeMemberships = await GetUserStoresMembershipsAsync(organizationId, userId);
        if (orgMemberships is not null && storeMemberships is not null)
        {
            throw new ValidationException($"User {userId} can not hold both an organization and store membership.");
        }

        // add the users memberships
        if (orgMemberships is not null)
        {
            userDto.UserType = MembershipScope.ORGANIZATION;
            userDto.Memberships = orgMemberships;
        }
        else if (storeMemberships is not null)
        {
            userDto.UserType = MembershipScope.STORE;
            userDto.Memberships = storeMemberships;
            // for store users dont send back the organizationId
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
}
