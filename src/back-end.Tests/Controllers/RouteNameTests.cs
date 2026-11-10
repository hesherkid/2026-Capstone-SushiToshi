using Xunit;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using back_end.Controllers;

namespace back_end.Tests.Controllers;

public class RouteNameTests
{
    /// <summary>
    /// The words in a route path are kebab-case, like api/table-group/{id}/active-session.
    /// Route parameters ({sessionId}) and the [controller] prefix (the class name, like Bill) aren't checked.
    /// </summary>
    private static readonly Regex KebabCase = new("^[a-z0-9]+(-[a-z0-9]+)*$");

    [Fact]
    public void EveryRouteSegment_IsKebabCase()
    {
        var controllers = typeof(BillController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        var notKebabCase = controllers
            .SelectMany(c => Templates(c).Concat(c.GetMethods().SelectMany(Templates))
                .Select(template => $"{c.Name}: {template}"))
            .Where(route => LiteralSegments(route[(route.IndexOf(": ") + 2)..]).Any(s => !KebabCase.IsMatch(s)))
            .Distinct()
            .ToList();

        Assert.Empty(notKebabCase);
    }

    private static IEnumerable<string> Templates(MemberInfo member) =>
        member.GetCustomAttributes(inherit: false)
            .OfType<IRouteTemplateProvider>()
            .Select(a => a.Template)
            .Where(t => !string.IsNullOrEmpty(t))
            .Cast<string>();

    // "api/[controller]/{tagId:int}/menu-items" -> "api", "menu-items"
    private static IEnumerable<string> LiteralSegments(string template) =>
        template.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !s.StartsWith('{') && !(s.StartsWith('[') && s.EndsWith(']')));
}
