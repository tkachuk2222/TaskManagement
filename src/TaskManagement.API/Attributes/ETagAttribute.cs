using Microsoft.AspNetCore.Mvc.Filters;
using TaskManagement.API.Filters;

namespace TaskManagement.API.Attributes;

/// <summary>
/// Attribute to enable ETag support on controller actions
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class ETagAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var etagService = serviceProvider.GetRequiredService<API.Services.IETagService>();
        return new ETagFilter(etagService);
    }
}
