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
    public Task<UserEntity?> GetUserByIdAsync(Guid organizationId, Guid userId)
    {
        return _userRepository.GetUserByIdAsync(organizationId, userId);
    }
}
