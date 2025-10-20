using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace TaskManagement.Infrastructure.Configuration;

/// <summary>
/// Configuration for Polly resilience policies (circuit breaker, retry)
/// </summary>
public static class PollyConfiguration
{
    /// <summary>
    /// Retry policy with exponential backoff
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx and 408
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning(
                        "Retry {RetryAttempt} after {Delay}s due to {StatusCode}. Request: {RequestUri}",
                        retryAttempt,
                        timespan.TotalSeconds,
                        outcome.Result?.StatusCode ?? 0,
                        outcome.Result?.RequestMessage?.RequestUri);
                });
    }

    /// <summary>
    /// Circuit breaker policy
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning(
                        "Circuit breaker opened for {Duration}s due to {StatusCode}. Request: {RequestUri}",
                        duration.TotalSeconds,
                        outcome.Result?.StatusCode ?? 0,
                        outcome.Result?.RequestMessage?.RequestUri);
                },
                onReset: context =>
                {
                    var logger = context.GetLogger();
                    logger?.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    // Optional: Log when circuit transitions to half-open
                });
    }

    /// <summary>
    /// Combined policy: Retry + Circuit Breaker
    /// Retry happens first, then circuit breaker tracks failures
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
    {
        var retry = GetRetryPolicy();
        var circuitBreaker = GetCircuitBreakerPolicy();
        
        // Wrap retry inside circuit breaker
        return Policy.WrapAsync(retry, circuitBreaker);
    }

    /// <summary>
    /// Timeout policy
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            timeout: TimeSpan.FromSeconds(10),
            onTimeoutAsync: (context, timespan, task) =>
            {
                var logger = context.GetLogger();
                logger?.LogWarning("Request timed out after {Timeout}s", timespan.TotalSeconds);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Full resilience policy: Timeout -> Retry -> Circuit Breaker
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetFullResiliencePolicy()
    {
        var timeout = GetTimeoutPolicy();
        var retry = GetRetryPolicy();
        var circuitBreaker = GetCircuitBreakerPolicy();

        // Order: timeout is innermost, then retry, then circuit breaker
        return Policy.WrapAsync(circuitBreaker, retry, timeout);
    }
}

/// <summary>
/// Extension methods for Polly context
/// </summary>
public static class PollyContextExtensions
{
    private const string LoggerKey = "ILogger";

    public static Context WithLogger(this Context context, ILogger logger)
    {
        context[LoggerKey] = logger;
        return context;
    }

    public static ILogger? GetLogger(this Context context)
    {
        if (context.TryGetValue(LoggerKey, out var logger))
        {
            return logger as ILogger;
        }
        return null;
    }
}
