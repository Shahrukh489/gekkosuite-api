using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

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
    public Guid? OrganizationId { get; set; }

    /// <summary>Account kill switch; false = every membership is suspended.</summary>
    public bool IsActive { get; set; }

    /// <summary>The user's type (ORGANIZATION or STORE), or null when they have no memberships yet.</summary>
    public MembershipScope? UserType { get; set; } = null;

    /// <summary>The user's memberships (org entry, or store entries oldest-first); empty when UserType is null.</summary>
    public List<MembershipDto> Memberships { get; set; } = [];

    ///<summary>Map from UserEntity to UserDto; derives UserType from the memberships and enforces the one-kind rule.</summary>
    public static UserDto FromEntity(UserEntity userEntity)
    {
        return new UserDto()
        {
            FirstName = userEntity.FirstName,
            LastName = userEntity.LastName,
            OrganizationId = userEntity.OrganizationId,
            Email = userEntity.Email,
            UserId = userEntity.UserId,
            IsActive = userEntity.IsActive,
            Memberships = MembershipDto.FromEntityList(userEntity.Memberships)
        };
    }

    /// <summary>Map from a UserEntity list to a UserDto list.</summary>
    public static List<UserDto> FromEntityList(List<UserEntity> userEntities)
    {
        List<UserDto> userDtos = new List<UserDto>();

        for (int i = 0; i < userEntities.Count; i++)
        {
            userDtos.Add(FromEntity(userEntities[i]));
        }

        return userDtos;
    }
}
