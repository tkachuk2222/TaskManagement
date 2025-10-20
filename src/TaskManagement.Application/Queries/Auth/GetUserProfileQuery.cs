using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Auth;

namespace TaskManagement.Application.Queries.Auth;

/// <summary>
/// Query to get user profile from Supabase
/// </summary>
public record GetUserProfileQuery(
    string UserId
) : IRequest<UserProfileResponse?>;

public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, UserProfileResponse?>
{
    private readonly IAuthService _authService;

    public GetUserProfileQueryHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<UserProfileResponse?> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        return await _authService.GetUserProfileAsync(request.UserId, cancellationToken);
    }
}
