using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TaskManagement.API.Services;

namespace TaskManagement.API.Filters;

/// <summary>
/// Action filter that automatically adds ETag headers to responses
/// and handles If-None-Match for 304 Not Modified responses
/// </summary>
public class ETagFilter : IActionFilter
{
    private readonly IETagService _etagService;

    public ETagFilter(IETagService etagService)
    {
        _etagService = etagService;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Nothing to do before action executes
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Only process successful GET requests
        if (context.HttpContext.Request.Method != HttpMethods.Get)
            return;

        if (context.Result is not ObjectResult objectResult)
            return;

        if (objectResult.StatusCode != 200 && objectResult.StatusCode != null)
            return;

        var value = objectResult.Value;
        if (value == null)
            return;

        // Generate ETag for the response
        var etag = _etagService.GenerateETag(value);
        
        // Add ETag to response headers
        context.HttpContext.Response.Headers.ETag = etag;

        // Check if client has matching ETag (If-None-Match)
        if (_etagService.IsNotModified(context.HttpContext.Request, etag))
        {
            // Return 304 Not Modified
            context.Result = new StatusCodeResult(304);
        }
    }
}
