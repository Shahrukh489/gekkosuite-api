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

        return new UserDto().FromEntity(userEntity);
    }

    /// <inheritdoc />
    public async Task<List<MembershipDto>?> GetUserOrganizationMembershipsAsync(Guid organizationId, Guid userId)
    {
        IEnumerable<MembershipEntity> membershipEntities = await _userRepository.GetUserOrganizationMembershipsAsync(organizationId, userId);
        if (membershipEntities.Count() == 0)
        {
            return null;
        }

        return new MembershipDto().FromEntityList(membershipEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<List<MembershipDto>?> GetUserStoreMembershipsAsync(Guid organizationId, Guid userId)
    {
        IEnumerable<MembershipEntity> membershipEntities = await _userRepository.GetUserStoreMembershipsAsync(organizationId, userId);
        if (membershipEntities.Count() == 0)
        {
            return null;
        }

        // @TODO: add LRU in-memory-cache so we dont have to run that large query for every logged in user

        //@TODO: this weird creating new signle dto and then frmo entitylsit
        return new MembershipDto().FromEntityList(membershipEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<List<MembershipDto>?> GetUserStoreMembershipsByStoreIdAsync(Guid organizationId, Guid userId, Guid storeId)
    {
        IEnumerable<MembershipEntity> membershipEntities = await _userRepository.GetUserStoreMembershipsByStoreIdAsync(organizationId, userId, storeId);
        if (membershipEntities.Count() == 0)
        {
            return null;
        }

        return new MembershipDto().FromEntityList(membershipEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserAsync(Guid organizationId, Guid userId)
    {
        // @TODO: add LRU in-memory-cache so we dont have to run that large query for every logged in user

        UserDto? userDto = await GetUserByIdAsync(organizationId, userId);
        if (userDto is null)
        {
            return null;
        }

        // A user is EITHER an org member OR a store member, never both (the one-kind rule).
        // If we detect both then we throw an Exception, something went wrong in our database that allowed this to happen
        var orgMemberships = await GetUserOrganizationMembershipsAsync(organizationId, userId);
        var storeMemberships = await GetUserStoreMembershipsAsync(organizationId, userId);
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
