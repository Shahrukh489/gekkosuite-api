using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Repositories;

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
    public async Task<MembershipDto?> GetOrganizationMembershipAsync(Guid organizationId, Guid userId)
    {
        MembershipEntity? membershipEntity = await _userRepository.GetOrganizationMembershipAsync(organizationId, userId);
        if (membershipEntity == null)
        {
            return null;
        }

        return new MembershipDto().FromEntity(membershipEntity);
    }

    /// <inheritdoc />
    public async Task<List<MembershipDto>?> GetStoreMembershipsAsync(Guid organizationId, Guid userId)
    {
        IEnumerable<MembershipEntity> membershipEntities = await _userRepository.GetStoreMembershipsAsync(organizationId, userId);
        if (membershipEntities.Count() == 0)
        {
            return null;
        }

        return new MembershipDto().FromEntityList(membershipEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserAsync(Guid organizationId, Guid userId)
    {
        UserDto? userDto = await GetUserByIdAsync(organizationId, userId);
        if (userDto is null)
        {
            return null;
        }

        // A user is EITHER an org member OR a store member, never both (the one-kind rule).
        // If we detect both then we throw an Exception, something went wrong in our database that allowed this to happen
        var orgMembership = await GetOrganizationMembershipAsync(organizationId, userId);
        var storeMemberships = await GetStoreMembershipsAsync(organizationId, userId);
        if (orgMembership is not null && storeMemberships is not null)
        {
            throw new InvalidOperationException($"User {userId} holds both an organization and store membership, violating the one-kind rule.");
        }


        if (orgMembership is not null)
        {
            userDto.UserType = Constants.ORGANIZATION;
            userDto.Memberships = new List<MembershipDto>() { orgMembership };
        }
        else if (storeMemberships is not null)
        {
            userDto.UserType = Constants.STORE;
            userDto.Memberships = storeMemberships;

            // @TODO: sort by oldest membership createdAt Timestamp
            // land in the oldest store membership — the query returns them oldest-first
            userDto.DefaultStoreId = storeMemberships[0].StoreId;

        }
        else
        {
            // if no org or store memberships user type is none, he can not access anything
            userDto.UserType = Constants.NONE;
            userDto.Memberships = null;
        }
        return userDto;
    }
}
