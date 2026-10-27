using back_end.Services;
using Microsoft.AspNetCore.Mvc;

namespace back_end.Helpers
{
    // The 403 responses for staff working outside their location (see ILocationAccessService)
    public static class LocationResults
    {
        public static ObjectResult NoLocation() =>
            new(ILocationAccessService.NoLocationMessage) { StatusCode = StatusCodes.Status403Forbidden };

        public static ObjectResult OtherLocation() =>
            new(ILocationAccessService.OtherLocationMessage) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
