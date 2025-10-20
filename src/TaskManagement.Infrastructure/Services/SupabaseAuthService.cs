using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Auth;
using TaskManagement.Infrastructure.Configuration;

namespace TaskManagement.Infrastructure.Services;

public class SupabaseAuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseSettings _settings;
    private static readonly JwtSecurityTokenHandler _jwtHandler = new();

    public SupabaseAuthService(HttpClient httpClient, IOptions<SupabaseSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress = new Uri($"{_settings.Url}/auth/v1/");
        _httpClient.DefaultRequestHeaders.Add("apikey", _settings.AnonKey);
    }

    public async Task<AuthResponse?> RegisterAsync(string email, string password, string fullName, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                email,
                password,
                data = new { full_name = fullName }
            };

            var response = await _httpClient.PostAsJsonAsync("signup", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Supabase registration failed: {response.StatusCode} - {error}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<SupabaseAuthResponse>(cancellationToken);
            
            // Check if we got a valid response with tokens
            if (result == null)
            {
                Console.WriteLine("Supabase returned null response");
                return null;
            }

            // Check if access token is missing (email confirmation required)
            if (string.IsNullOrEmpty(result.AccessToken))
            {
                Console.WriteLine($"Supabase registration succeeded but no access_token returned.");
                Console.WriteLine($"This usually means email confirmation is enabled.");
                Console.WriteLine($"Please disable email confirmation in Supabase Dashboard:");
                Console.WriteLine($"  Authentication → Providers → Email → Uncheck 'Confirm email'");
                return null;
            }
            
            return MapToAuthResponse(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during registration: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { email, password, grant_type = "password" };

            var response = await _httpClient.PostAsJsonAsync("token?grant_type=password", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Supabase login failed: {response.StatusCode} - {error}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<SupabaseAuthResponse>(cancellationToken);
            
            return result != null ? MapToAuthResponse(result) : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during login: {ex.Message}");
            return null;
        }
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { refresh_token = refreshToken };

            var response = await _httpClient.PostAsJsonAsync("token?grant_type=refresh_token", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Supabase token refresh failed: {response.StatusCode} - {error}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<SupabaseAuthResponse>(cancellationToken);
            
            return result != null ? MapToAuthResponse(result) : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during token refresh: {ex.Message}");
            return null;
        }
    }

    public async Task<UserProfileResponse?> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try admin endpoint first (requires service role key)
            var response = await _httpClient.GetAsync($"admin/users/{userId}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<SupabaseUserDetails>(cancellationToken);
                
                if (user != null)
                {
                    return new UserProfileResponse
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FullName = user.UserMetadata?.FullName ?? user.Email,
                        CreatedAt = user.CreatedAt
                    };
                }
            }

            // Admin endpoint failed - this is expected with anon key
            // Return minimal profile indicating the user exists but we need a proper access token
            return new UserProfileResponse
            {
                Id = userId,
                Email = "N/A - Requires Access Token",
                FullName = "N/A - Requires Access Token",
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception fetching user profile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extract user profile information from JWT access token
    /// This should be used instead of GetUserProfileAsync when you have an access token
    /// </summary>
    public static UserProfileResponse? ExtractUserProfileFromToken(string accessToken)
    {
        try
        {
            if (string.IsNullOrEmpty(accessToken))
                return null;

            var token = _jwtHandler.ReadJwtToken(accessToken);

            var userId = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            var email = token.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var fullName = token.Claims.FirstOrDefault(c => c.Type == "user_metadata" || c.Type == "name")?.Value;
            
            // Try to parse created_at from auth_time or iat
            var authTime = token.Claims.FirstOrDefault(c => c.Type == "auth_time")?.Value;
            var iat = token.Claims.FirstOrDefault(c => c.Type == "iat")?.Value;
            
            DateTime createdAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(authTime) && long.TryParse(authTime, out var authTimeSeconds))
            {
                createdAt = DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds).UtcDateTime;
            }
            else if (!string.IsNullOrEmpty(iat) && long.TryParse(iat, out var iatSeconds))
            {
                createdAt = DateTimeOffset.FromUnixTimeSeconds(iatSeconds).UtcDateTime;
            }

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
                return null;

            return new UserProfileResponse
            {
                Id = userId,
                Email = email,
                FullName = fullName ?? email.Split('@')[0],
                CreatedAt = createdAt
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to extract user profile from JWT: {ex.Message}");
            return null;
        }
    }

    private AuthResponse MapToAuthResponse(SupabaseAuthResponse response)
    {
        return new AuthResponse
        {
            AccessToken = response.AccessToken,
            RefreshToken = response.RefreshToken ?? string.Empty,
            UserId = response.User?.Id ?? string.Empty,
            Email = response.User?.Email ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(response.ExpiresIn)
        };
    }
}

internal class SupabaseAuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("user")]
    public SupabaseUser? User { get; set; }
}

internal class SupabaseUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;
}

internal class SupabaseUserDetails
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("user_metadata")]
    public UserMetadata? UserMetadata { get; set; }
}

internal class UserMetadata
{
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }
}
