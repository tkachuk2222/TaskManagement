namespace TaskManagement.Contracts.Auth;

public class LogoutRequest
{
    public string RefreshToken { get; set; } = null!;
}

public class SessionResponse
{
    public string Id { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceType { get; set; }
    public string? OS { get; set; }
    public string? Browser { get; set; }
    public bool IsCurrentSession { get; set; }
}

public class RevokeSessionRequest
{
    public string SessionId { get; set; } = null!;
}
