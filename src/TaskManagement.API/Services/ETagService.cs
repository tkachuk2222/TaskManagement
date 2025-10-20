using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaskManagement.API.Services;

/// <summary>
/// Service for generating and validating ETags for HTTP caching and optimistic concurrency
/// </summary>
public interface IETagService
{
    /// <summary>
    /// Generate ETag from an object
    /// </summary>
    string GenerateETag<T>(T obj) where T : class;
    
    /// <summary>
    /// Validate if the provided ETag matches the current object
    /// </summary>
    bool ValidateETag<T>(T obj, string? etag) where T : class;
    
    /// <summary>
    /// Check if request has If-None-Match header and it matches the ETag
    /// </summary>
    bool IsNotModified(HttpRequest request, string etag);
}

public class ETagService : IETagService
{
    public string GenerateETag<T>(T obj) where T : class
    {
        if (obj == null)
            return string.Empty;

        // Serialize object to JSON for consistent hashing
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        // Generate MD5 hash
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(json));
        
        // Convert to hex string and wrap in quotes (ETag format)
        var etag = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return $"\"{etag}\"";
    }

    public bool ValidateETag<T>(T obj, string? etag) where T : class
    {
        if (string.IsNullOrEmpty(etag))
            return false;

        var currentETag = GenerateETag(obj);
        return string.Equals(currentETag, etag, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsNotModified(HttpRequest request, string etag)
    {
        // Check If-None-Match header (used for GET requests)
        var ifNoneMatch = request.Headers.IfNoneMatch.ToString();
        
        if (string.IsNullOrEmpty(ifNoneMatch))
            return false;

        // Handle multiple ETags (comma-separated)
        var etags = ifNoneMatch.Split(',').Select(e => e.Trim()).ToArray();
        
        // Check if any ETag matches or if wildcard (*) is used
        return etags.Contains("*") || etags.Contains(etag, StringComparer.OrdinalIgnoreCase);
    }
}
