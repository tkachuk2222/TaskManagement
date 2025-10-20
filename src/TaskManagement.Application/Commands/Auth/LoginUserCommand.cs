using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Auth;

namespace TaskManagement.Application.Commands.Auth;

/// <summary>
/// Command to authenticate a user via Supabase
/// </summary>
public record LoginUserCommand(
    string Email,
    string Password
) : IRequest<AuthResponse?>;

public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, AuthResponse?>
{
    private readonly IAuthService _authService;

    public LoginUserCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<AuthResponse?> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        return await _authService.LoginAsync(request.Email, request.Password, cancellationToken);
    }
}
