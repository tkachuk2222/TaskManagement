using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManagement.Application.Commands.Auth;
using TaskManagement.Application.Queries.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Auth;
using TaskManagement.Infrastructure.Services;
using System.Security.Cryptography;
using System.Text;

namespace TaskManagement.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[EnableRateLimiting("authentication")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthService _authService;

    public AuthController(IMediator mediator, IAuthService authService)
    {
        _mediator = mediator;
        _authService = authService;
    }

    private string? GetUserIdFromToken()
    {
        // Try multiple claim types to handle different JWT configurations
        return User.FindFirst("sub")?.Value 
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
    }

    private string? GetAccessTokenFromHeader()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(request.Email, request.Password, request.FullName);
        var result = await _mediator.Send(command, cancellationToken);
        
        if (result == null)
            return BadRequest(new { error = "Registration failed" });

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginUserCommand(request.Email, request.Password);
        var result = await _mediator.Send(command, cancellationToken);
        
        if (result == null)
            return Unauthorized(new { error = "Invalid credentials" });

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await _mediator.Send(command, cancellationToken);
        
        if (result == null)
            return Unauthorized(new { error = "Invalid or expired refresh token" });

        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        if (_authService is SessionManagedAuthService sessionService)
        {
            var success = await sessionService.LogoutAsync(request.RefreshToken, cancellationToken);
            
            if (!success)
                return BadRequest(new { error = "Invalid session" });

            return Ok(new { message = "Logged out successfully" });
        }

        return Ok(new { message = "Logged out" });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult GetProfile()
    {
        var userId = GetUserIdFromToken();
        
        if (string.IsNullOrEmpty(userId))
        {
            // Log all claims for debugging
            var claims = string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}"));
            Console.WriteLine($"Available claims: {claims}");
            return Unauthorized(new { error = "User ID not found in token" });
        }

        // Extract user info directly from JWT claims
        var email = User.FindFirst("email")?.Value 
                 ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
                 ?? "unknown@example.com";
        
        var fullName = User.FindFirst("user_metadata")?.Value
                    ?? User.FindFirst("full_name")?.Value
                    ?? email.Split('@')[0];

        // Try to extract full_name from user_metadata JSON if available
        var userMetadataClaim = User.FindFirst(c => c.Type.Contains("user_metadata"))?.Value;
        if (!string.IsNullOrEmpty(userMetadataClaim))
        {
            try
            {
                var metadata = System.Text.Json.JsonDocument.Parse(userMetadataClaim);
                if (metadata.RootElement.TryGetProperty("full_name", out var fullNameProp))
                {
                    fullName = fullNameProp.GetString() ?? fullName;
                }
            }
            catch
            {
                // Ignore JSON parsing errors
            }
        }

        // Try to get auth_time or iat for creation timestamp
        var authTime = User.FindFirst("auth_time")?.Value;
        var iat = User.FindFirst("iat")?.Value;
        
        DateTime createdAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(authTime) && long.TryParse(authTime, out var authTimeSeconds))
        {
            createdAt = DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds).UtcDateTime;
        }
        else if (!string.IsNullOrEmpty(iat) && long.TryParse(iat, out var iatSeconds))
        {
            createdAt = DateTimeOffset.FromUnixTimeSeconds(iatSeconds).UtcDateTime;
        }

        var profile = new UserProfileResponse
        {
            Id = userId,
            Email = email,
            FullName = fullName,
            CreatedAt = createdAt
        };

        return Ok(profile);
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (_authService is SessionManagedAuthService sessionService)
        {
            var sessions = await sessionService.GetUserSessionsAsync(userId, cancellationToken);
            
            // Get current access token hash to identify current session
            var currentAccessToken = GetAccessTokenFromHeader();
            var currentTokenHash = !string.IsNullOrEmpty(currentAccessToken) 
                ? ComputeHash(currentAccessToken) 
                : null;
            
            var sessionResponses = sessions.Select(s => new SessionResponse
            {
                Id = s.Id,
                CreatedAt = s.CreatedAt,
                LastActivityAt = s.LastActivityAt,
                IpAddress = s.IpAddress,
                DeviceType = s.DeviceInfo?.DeviceType,
                OS = s.DeviceInfo?.OS,
                Browser = s.DeviceInfo?.Browser,
                IsCurrentSession = currentTokenHash != null && s.AccessTokenHash == currentTokenHash
            });

            return Ok(sessionResponses);
        }

        return Ok(Array.Empty<SessionResponse>());
    }

    [Authorize]
    [HttpPost("sessions/revoke")]
    public async Task<IActionResult> RevokeSession([FromBody] RevokeSessionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (_authService is SessionManagedAuthService sessionService)
        {
            await sessionService.RevokeSessionAsync(request.SessionId, cancellationToken);
            return Ok(new { message = "Session revoked successfully" });
        }

        return Ok(new { message = "Session revoked" });
    }

    [Authorize]
    [HttpPost("sessions/revoke-all")]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (_authService is SessionManagedAuthService sessionService)
        {
            await sessionService.RevokeAllSessionsAsync(userId, cancellationToken);
            return Ok(new { message = "All sessions revoked successfully" });
        }

        return Ok(new { message = "All sessions revoked" });
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
