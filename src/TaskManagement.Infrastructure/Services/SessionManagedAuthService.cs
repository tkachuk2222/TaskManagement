using System.Security.Cryptography;
using System.Text;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Auth;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Interfaces;
using UAParser;

namespace TaskManagement.Infrastructure.Services;

/// <summary>
/// Enhanced authentication service with session management
/// </summary>
public class SessionManagedAuthService : IAuthService
{
    private readonly IAuthService _supabaseAuthService;
    private readonly IUserSessionRepository _sessionRepository;
    private static readonly Parser _uaParser = Parser.GetDefault();

    public SessionManagedAuthService(
        SupabaseAuthService supabaseAuthService,
        IUserSessionRepository sessionRepository)
    {
        _supabaseAuthService = supabaseAuthService;
        _sessionRepository = sessionRepository;
    }

    public async Task<AuthResponse?> RegisterAsync(string email, string password, string fullName, CancellationToken cancellationToken = default)
    {
        // Register with Supabase
        var authResponse = await _supabaseAuthService.RegisterAsync(email, password, fullName, cancellationToken);
        
        if (authResponse == null || string.IsNullOrEmpty(authResponse.AccessToken))
            return null;

        // Create session in our database
        await CreateSessionAsync(authResponse, null, null, cancellationToken);

        return authResponse;
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        // Login with Supabase
        var authResponse = await _supabaseAuthService.LoginAsync(email, password, cancellationToken);
        
        if (authResponse == null || string.IsNullOrEmpty(authResponse.AccessToken))
            return null;

        // Create session in our database
        await CreateSessionAsync(authResponse, null, null, cancellationToken);

        return authResponse;
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // Find session by refresh token
        var session = await _sessionRepository.GetSessionByRefreshTokenAsync(refreshToken, cancellationToken);
        
        if (session == null || !session.IsActive)
        {
            Console.WriteLine("Session not found or inactive");
            return null;
        }

        // Check if session expired
        if (session.ExpiresAt < DateTime.UtcNow)
        {
            await _sessionRepository.RevokeSessionAsync(session.Id, "Expired", cancellationToken);
            Console.WriteLine("Session expired");
            return null;
        }

        // Refresh token with Supabase
        var authResponse = await _supabaseAuthService.RefreshTokenAsync(refreshToken, cancellationToken);
        
        if (authResponse == null || string.IsNullOrEmpty(authResponse.AccessToken))
        {
            // Revoke session if refresh failed
            await _sessionRepository.RevokeSessionAsync(session.Id, "Refresh failed", cancellationToken);
            return null;
        }

        // Update session with new tokens
        var newAccessTokenHash = ComputeHash(authResponse.AccessToken);
        await _sessionRepository.UpdateRefreshTokenAsync(
            session.Id,
            authResponse.RefreshToken,
            newAccessTokenHash,
            authResponse.ExpiresAt.AddDays(30), // Refresh token valid for 30 days
            cancellationToken);

        return authResponse;
    }

    public async Task<UserProfileResponse?> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _supabaseAuthService.GetUserProfileAsync(userId, cancellationToken);
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetSessionByRefreshTokenAsync(refreshToken, cancellationToken);
        
        if (session == null)
            return false;

        await _sessionRepository.RevokeSessionAsync(session.Id, "User logout", cancellationToken);
        return true;
    }

    public async Task<IEnumerable<UserSession>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _sessionRepository.GetActiveSessionsByUserIdAsync(userId, cancellationToken);
    }

    public async Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _sessionRepository.RevokeSessionAsync(sessionId, "User revoked", cancellationToken);
    }

    public async Task RevokeAllSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _sessionRepository.RevokeAllUserSessionsAsync(userId, "User revoked all", cancellationToken);
    }

    private async Task CreateSessionAsync(
        AuthResponse authResponse,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var session = new UserSession
        {
            UserId = authResponse.UserId,
            Email = authResponse.Email,
            RefreshToken = authResponse.RefreshToken,
            AccessTokenHash = ComputeHash(authResponse.AccessToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = authResponse.ExpiresAt.AddDays(30), // Refresh token valid for 30 days
            LastActivityAt = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = ParseUserAgent(userAgent),
            IsActive = true
        };

        await _sessionRepository.CreateSessionAsync(session, cancellationToken);
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Parse user agent string using UAParser library for accurate device detection
    /// </summary>
    private static DeviceInfo? ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return null;

        try
        {
            var clientInfo = _uaParser.Parse(userAgent);

            var deviceType = DetermineDeviceType(clientInfo);
            var os = FormatOS(clientInfo.OS);
            var browser = FormatBrowser(clientInfo.UA);

            return new DeviceInfo
            {
                DeviceType = deviceType,
                OS = os,
                Browser = browser
            };
        }
        catch
        {
            // Fallback to basic parsing if UAParser fails
            return new DeviceInfo
            {
                DeviceType = "Unknown",
                OS = "Unknown",
                Browser = "Unknown"
            };
        }
    }

    private static string DetermineDeviceType(ClientInfo clientInfo)
    {
        var deviceFamily = clientInfo.Device.Family?.ToLower() ?? "";

        if (deviceFamily.Contains("ipad") || deviceFamily.Contains("tablet"))
            return "Tablet";
        
        if (deviceFamily.Contains("iphone") || 
            deviceFamily.Contains("mobile") || 
            deviceFamily.Contains("android") ||
            !string.IsNullOrEmpty(clientInfo.Device.Model))
            return "Mobile";

        return "Desktop";
    }

    private static string FormatOS(OS os)
    {
        if (string.IsNullOrEmpty(os.Family))
            return "Unknown";

        var version = "";
        if (!string.IsNullOrEmpty(os.Major))
        {
            version = $" {os.Major}";
            if (!string.IsNullOrEmpty(os.Minor))
                version += $".{os.Minor}";
        }

        return $"{os.Family}{version}";
    }

    private static string FormatBrowser(UserAgent ua)
    {
        if (string.IsNullOrEmpty(ua.Family))
            return "Unknown";

        var version = "";
        if (!string.IsNullOrEmpty(ua.Major))
        {
            version = $" {ua.Major}";
            if (!string.IsNullOrEmpty(ua.Minor))
                version += $".{ua.Minor}";
        }

        return $"{ua.Family}{version}";
    }
}
