using GekkoSuite.Api.Entities;

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

    /// <summary>"ORGANIZATION", "STORE", or null when the user has no memberships yet.</summary>
    public string UserType { get; set; } = string.Empty;

    /// <summary>The user's memberships (org entry, or store entries oldest-first); empty when UserType is null.</summary>
    public List<MembershipDto>? Memberships { get; set; } = [];

    ///<summary>Map from UserEntity to UserDto </summary> 
    public UserDto FromEntity(UserEntity userEntity)
    {
        return new UserDto()
        {
            FirstName = userEntity.FirstName,
            LastName = userEntity.LastName,
            OrganizationId = userEntity.OrganizationId,
            Email = userEntity.Email,
            UserId = userEntity.UserId,
            IsActive = userEntity.IsActive
        };
    }
}
