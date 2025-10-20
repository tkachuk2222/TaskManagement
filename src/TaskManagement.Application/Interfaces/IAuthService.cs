using TaskManagement.Contracts.Auth;

namespace TaskManagement.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse?> RegisterAsync(string email, string password, string fullName, CancellationToken cancellationToken = default);
    Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<UserProfileResponse?> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default);
}
