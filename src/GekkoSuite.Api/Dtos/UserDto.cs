using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Exceptions;

namespace GekkoSuite.Api.Dtos;

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

    /// <summary>Account kill switch; false = every membership is suspended.</summary>
    public bool IsActive { get; set; }

    /// <summary>The user's type (ORGANIZATION or STORE), or null when they have no memberships yet.</summary>
    public MembershipScope? UserType { get; set; } = null;

    /// <summary>The user's memberships (org entry, or store entries oldest-first); empty when UserType is null.</summary>
    public List<MembershipDto> Memberships { get; set; } = [];

    ///<summary>Map from UserEntity to UserDto; derives UserType from the memberships and enforces the one-kind rule.</summary>
    public static UserDto FromEntity(UserEntity userEntity)
    {
        // A user is EITHER an org member OR a store member, never both (the one-kind rule). Both present
        // means the database allowed an invalid state — fail loudly rather than return a corrupt view.
        bool hasOrg = userEntity.Memberships.Any(membership => membership.Scope == MembershipScope.ORGANIZATION);
        bool hasStore = userEntity.Memberships.Any(membership => membership.Scope == MembershipScope.STORE);
        if (hasOrg && hasStore)
        {
            throw new ValidationException($"User {userEntity.UserId} can not hold both an organization and store membership.");
        }

        MembershipScope? userType = hasOrg ? MembershipScope.ORGANIZATION : hasStore ? MembershipScope.STORE : null;

        return new UserDto()
        {
            FirstName = userEntity.FirstName,
            LastName = userEntity.LastName,
            OrganizationId = userEntity.OrganizationId,
            Email = userEntity.Email,
            UserId = userEntity.UserId,
            IsActive = userEntity.IsActive,
            UserType = userType,
            Memberships = MembershipDto.FromEntityList(userEntity.Memberships)
        };
    }
}
