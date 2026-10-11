using back_end.Services.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace back_end.controllers
{
    internal static class AnalyticsActionResults
    {
        /// <summary>Ok - 200, Invalid - 400 ValidationProblemDetails (keyed by query parameter), NotFound - 404.</summary>
        public static IActionResult ToActionResult<T>(this ControllerBase controller, AnalyticsResult<T> result)
        {
            switch (result.Status)
            {
                case AnalyticsResultStatus.Ok:
                    return controller.Ok(result.Value);

                case AnalyticsResultStatus.Invalid:
                    controller.ModelState.AddModelError(result.ErrorKey, result.ErrorMessage);
                    return controller.ValidationProblem(controller.ModelState);

                case AnalyticsResultStatus.NotFound:
                    return controller.NotFound(new { message = result.ErrorMessage });

                default:
                    throw new InvalidOperationException($"Unhandled analytics result status {result.Status}.");
            }
        }
    }
}