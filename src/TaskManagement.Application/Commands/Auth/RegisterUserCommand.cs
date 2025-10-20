using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Auth;

namespace TaskManagement.Application.Commands.Auth;

/// <summary>
/// Command to register a new user via Supabase
/// </summary>
public record RegisterUserCommand(
    string Email,
    string Password,
    string FullName
) : IRequest<AuthResponse?>;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, AuthResponse?>
{
    private readonly IAuthService _authService;

    public RegisterUserCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<AuthResponse?> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        return await _authService.RegisterAsync(request.Email, request.Password, request.FullName, cancellationToken);
    }
}
