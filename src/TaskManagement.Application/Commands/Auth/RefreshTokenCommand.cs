using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Auth;

namespace TaskManagement.Application.Commands.Auth;

/// <summary>
/// Command to refresh an expired access token using a refresh token
/// </summary>
public record RefreshTokenCommand(
    string RefreshToken
) : IRequest<AuthResponse?>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse?>
{
    private readonly IAuthService _authService;

    public RefreshTokenCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<AuthResponse?> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        return await _authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
    }
}
