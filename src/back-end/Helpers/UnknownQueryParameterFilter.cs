using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace back_end.Helpers;

// In development, a query parameter the action doesn't take gets a 400 naming it. Binding ignores
// case but not underscores, so an old ?locationId= call would otherwise quietly stop filtering.
// Program.cs adds it when Query:RejectUnknownParameters is on
public class UnknownQueryParameterFilter : IResourceFilter
{
    private readonly IModelMetadataProvider _metadata;
    // The query parameter names each action takes, by action id
    private static readonly ConcurrentDictionary<string, HashSet<string>> AllowedByAction = new();

    public UnknownQueryParameterFilter(IModelMetadataProvider metadata)
    {
        _metadata = metadata;
    }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (context.HttpContext.Request.Query.Count == 0) return;

        var allowed = AllowedByAction.GetOrAdd(context.ActionDescriptor.Id, _ => QueryNames(context.ActionDescriptor.Parameters, _metadata));
        var unknown = context.HttpContext.Request.Query.Keys.Where(key => !allowed.Contains(key)).ToList();
        if (unknown.Count == 0) return;

        var takes = allowed.Count == 0 ? "no query parameters" : string.Join(", ", allowed.Order());
        var errors = unknown.ToDictionary(
            key => key,
            key => new[] { $"This endpoint doesn't take the query parameter '{key}'. It takes {takes}." });
        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors));
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }

    // The query parameter names an action takes. [ApiController] has already set each parameter's
    // binding source, so a query parameter is either a simple value or a class whose properties
    // come from the query. Public so the tests can check every action's names
    public static HashSet<string> QueryNames(IEnumerable<ParameterDescriptor> parameters, IModelMetadataProvider metadataProvider)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            if (parameter.BindingInfo?.BindingSource != BindingSource.Query) continue;

            var metadata = metadataProvider.GetMetadataForType(parameter.ParameterType);
            if (!metadata.IsComplexType)
            {
                names.Add(parameter.BindingInfo.BinderModelName ?? parameter.Name);
                continue;
            }
            foreach (var property in metadata.Properties)
                names.Add(property.BinderModelName ?? property.Name ?? property.PropertyName!);
        }
        return names;
    }
}
