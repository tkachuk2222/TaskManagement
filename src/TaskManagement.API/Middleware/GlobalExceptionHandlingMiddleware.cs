using System.Net;
using System.Text.Json;
using FluentValidation;
using TaskManagement.Contracts.Common;

namespace TaskManagement.API.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        HttpStatusCode statusCode;
        object response;

        switch (exception)
        {
            case ValidationException validationException:
                statusCode = HttpStatusCode.BadRequest;
                response = new
                {
                    isSuccess = false,
                    error = "Validation failed",
                    errors = validationException.Errors.Select(e => new
                    {
                        propertyName = e.PropertyName,
                        errorMessage = e.ErrorMessage,
                        attemptedValue = e.AttemptedValue
                    }).ToList()
                };
                break;

            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Unauthorized;
                response = new
                {
                    isSuccess = false,
                    error = "Unauthorized access"
                };
                break;

            case KeyNotFoundException:
                statusCode = HttpStatusCode.NotFound;
                response = new
                {
                    isSuccess = false,
                    error = "Resource not found"
                };
                break;

            case ArgumentException argumentException:
                statusCode = HttpStatusCode.BadRequest;
                response = new
                {
                    isSuccess = false,
                    error = argumentException.Message
                };
                break;

            case InvalidOperationException invalidOperationException:
                statusCode = HttpStatusCode.BadRequest;
                response = new
                {
                    isSuccess = false,
                    error = invalidOperationException.Message
                };
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                response = new
                {
                    isSuccess = false,
                    error = "An internal server error occurred. Please try again later."
                };
                break;
        }

        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(response, options);
        return context.Response.WriteAsync(json);
    }
}
