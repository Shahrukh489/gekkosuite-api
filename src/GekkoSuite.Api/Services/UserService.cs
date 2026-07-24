using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class MembershipDto
{
    /// <summary>"ORGANIZATION" or "STORE".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The organization id (set for an ORGANIZATION membership).</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>The store id (set for a STORE membership).</summary>
    public Guid? StoreId { get; set; }

    /// <summary>The place's display name — the org name or the store name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The role the user holds at this place.</summary>
    public string Role { get; set; } = string.Empty;
}

public class UserDto
{
    /// <summary>The user's id.</summary>
    public Guid UserId { get; set; }

    /// <summary>The user's first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>The user's last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>The user's login email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The user's organization (tenant).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>"ORGANIZATION", "STORE", or null when the user has no memberships yet.</summary>
    public string? UserType { get; set; }

    /// <summary>For a store user, the store to land in (oldest membership); null otherwise.</summary>
    public Guid? DefaultStoreId { get; set; }

    /// <summary>The user's memberships (org entry, or store entries oldest-first); empty when UserType is null.</summary>
    public List<MembershipDto> Memberships { get; set; } = [];
}

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public Task<UserEntity?> GetUserByIdAsync(Guid organizationId, Guid userId)
    {
        return _userRepository.GetUserByIdAsync(organizationId, userId);
    }

    /// <inheritdoc />
    public Task<MembershipEntity?> GetOrgMembershipAsync(Guid organizationId, Guid userId)
    {
        return _userRepository.GetOrgMembershipAsync(organizationId, userId);
    }

    /// <inheritdoc />
    public Task<IEnumerable<MembershipEntity>> GetStoreMembershipsAsync(Guid organizationId, Guid userId)
    {
        return _userRepository.GetStoreMembershipsAsync(organizationId, userId);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserAsync(Guid organizationId, Guid userId)
    {
        var user = await _userRepository.GetUserByIdAsync(organizationId, userId);
        if (user is null)
        {
            return null;
        }

        var dto = new UserDto
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            OrganizationId = user.OrganizationId
        };

        // A user is EITHER an org member OR a store member, never both (the one-kind rule).
        var orgMembership = await _userRepository.GetOrgMembershipAsync(organizationId, userId);
        var storeMemberships = (await _userRepository.GetStoreMembershipsAsync(organizationId, userId)).ToList();

        // If we detect both then we throw an Exception, something went wrong in our database that allowed this to happen
        if (orgMembership is not null && storeMemberships.Count > 0)
        {
            throw new InvalidOperationException(
                $"User {userId} holds both an organization and store membership, violating the one-kind rule.");
        }

        if (orgMembership is not null)
        {
            dto.UserType = "ORGANIZATION";
            dto.Memberships =
            [
                new MembershipDto
                {
                    Type = "ORGANIZATION",
                    OrganizationId = orgMembership.OrganizationId,
                    Name = orgMembership.Name,
                    Role = orgMembership.RoleName
                }
            ];
            
            return dto;
        }

        if (storeMemberships.Count > 0)
        {
            dto.UserType = "STORE";
            // land in the oldest store membership — the query returns them oldest-first
            dto.DefaultStoreId = storeMemberships[0].StoreId;
            dto.Memberships = storeMemberships
                .Select(s => new MembershipDto
                {
                    Type = "STORE",
                    StoreId = s.StoreId,
                    Name = s.Name,
                    Role = s.RoleName
                })
                .ToList();
                
            return dto;
        }

        // no memberships yet — a valid state, not an error (UserType stays null, empty list)
        return dto;
    }
}
