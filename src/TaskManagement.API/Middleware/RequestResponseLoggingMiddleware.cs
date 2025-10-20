using System.Diagnostics;
using System.Text;

namespace TaskManagement.API.Middleware;

/// <summary>
/// Middleware for comprehensive request/response logging
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for health checks and static files
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Items["X-Correlation-ID"]?.ToString() ?? "N/A";

        // Log request
        await LogRequest(context, correlationId);

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            stopwatch.Stop();

            // Log response
            await LogResponse(context, stopwatch.ElapsedMilliseconds, correlationId);
        }
        finally
        {
            // Copy response back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }

    private async Task LogRequest(HttpContext context, string correlationId)
    {
        var request = context.Request;
        
        var logMessage = new StringBuilder();
        logMessage.AppendLine($"HTTP Request [{correlationId}]");
        logMessage.AppendLine($"  Method: {request.Method}");
        logMessage.AppendLine($"  Path: {request.Path}{request.QueryString}");
        logMessage.AppendLine($"  Remote IP: {context.Connection.RemoteIpAddress}");
        logMessage.AppendLine($"  User-Agent: {request.Headers.UserAgent}");
        
        // Log authentication info if present
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            logMessage.AppendLine($"  User: {context.User.Identity.Name}");
        }

        // Log request body for non-GET requests (be careful with sensitive data)
        if (request.Method != "GET" && request.ContentLength > 0 && request.ContentLength < 10000)
        {
            request.EnableBuffering();
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length));
            var bodyAsText = Encoding.UTF8.GetString(buffer);
            
            // Mask sensitive fields
            bodyAsText = MaskSensitiveData(bodyAsText);
            logMessage.AppendLine($"  Body: {bodyAsText}");
            
            request.Body.Seek(0, SeekOrigin.Begin);
        }

        _logger.LogInformation(logMessage.ToString());
    }

    private async Task LogResponse(HttpContext context, long elapsedMs, string correlationId)
    {
        var response = context.Response;
        
        var logMessage = new StringBuilder();
        logMessage.AppendLine($"HTTP Response [{correlationId}]");
        logMessage.AppendLine($"  Status Code: {response.StatusCode}");
        logMessage.AppendLine($"  Content-Type: {response.ContentType}");
        logMessage.AppendLine($"  Duration: {elapsedMs}ms");

        // Log response body for errors (be careful with size)
        if (response.StatusCode >= 400 && response.Body.CanRead)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            var bodyAsText = await new StreamReader(response.Body).ReadToEndAsync();
            
            if (bodyAsText.Length > 1000)
                bodyAsText = bodyAsText[..1000] + "... (truncated)";
            
            logMessage.AppendLine($"  Body: {bodyAsText}");
            response.Body.Seek(0, SeekOrigin.Begin);
        }

        var logLevel = response.StatusCode >= 500 ? LogLevel.Error :
                       response.StatusCode >= 400 ? LogLevel.Warning :
                       elapsedMs > 5000 ? LogLevel.Warning : // Slow request warning
                       LogLevel.Information;

        _logger.Log(logLevel, logMessage.ToString());

        // Log slow queries separately for monitoring
        if (elapsedMs > 1000)
        {
            _logger.LogWarning("Slow request detected: {Method} {Path} took {Duration}ms [CorrelationId: {CorrelationId}]",
                context.Request.Method,
                context.Request.Path,
                elapsedMs,
                correlationId);
        }
    }

    private string MaskSensitiveData(string data)
    {
        // Mask common sensitive field patterns
        var sensitivePatterns = new[]
        {
            (@"""password""\s*:\s*""[^""]*""", @"""password"":""***MASKED***"""),
            (@"""refreshToken""\s*:\s*""[^""]*""", @"""refreshToken"":""***MASKED***"""),
            (@"""token""\s*:\s*""[^""]*""", @"""token"":""***MASKED***"""),
            (@"""secret""\s*:\s*""[^""]*""", @"""secret"":""***MASKED***""")
        };

        foreach (var (pattern, replacement) in sensitivePatterns)
        {
            data = System.Text.RegularExpressions.Regex.Replace(data, pattern, replacement, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return data;
    }
}
