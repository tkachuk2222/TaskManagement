using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TaskManagement.API.Services;

namespace TaskManagement.API.Attributes;

/// <summary>
/// Attribute to validate If-Match header for optimistic concurrency control
/// Used on PUT/PATCH/DELETE operations
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ValidateETagAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        
        // Only validate for PUT, PATCH, DELETE
        if (request.Method != HttpMethods.Put && 
            request.Method != HttpMethods.Patch && 
            request.Method != HttpMethods.Delete)
        {
            await next();
            return;
        }

        // Check if If-Match header is present
        var ifMatch = request.Headers.IfMatch.ToString();
        
        if (string.IsNullOrEmpty(ifMatch))
        {
            context.Result = new ObjectResult(new
            {
                error = "Precondition Required",
                message = "If-Match header is required for this operation. Please provide the current ETag."
            })
            {
                StatusCode = 428 // Precondition Required
            };
            return;
        }

        // Store If-Match value in HttpContext for command handlers to validate
        // The actual ETag validation should happen in the command handler
        // where we have access to the current state before modification
        context.HttpContext.Items["If-Match"] = ifMatch;

        await next();
    }
}
