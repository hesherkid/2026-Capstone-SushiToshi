using System.Security.Claims;

namespace back_end.Helpers
{
    public static class ClaimsHelpers
    {
        public static string GetUserId(ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.FindFirst("id")?.Value
                ?? user.FindFirst("user_id")?.Value
                ?? string.Empty;
        }

        public static string GetUserDisplayName(ClaimsPrincipal user)
        {
            // Prefer given_name + family_name if either is present, else fallback to name
            // The JWT handler renames given_name/family_name to ClaimTypes.GivenName/Surname
            // when it reads a token, so check both forms
            var given = (user.FindFirst("given_name") ?? user.FindFirst(ClaimTypes.GivenName))?.Value?.Trim();
            var family = (user.FindFirst("family_name") ?? user.FindFirst(ClaimTypes.Surname))?.Value?.Trim();
            if (!string.IsNullOrEmpty(given) || !string.IsNullOrEmpty(family))
            {
                var full = $"{given} {family}".Trim();
                return string.IsNullOrWhiteSpace(full) ? "Unknown" : full;
            }
            var name = user.FindFirst(ClaimTypes.Name)?.Value?.Trim();
            return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
        }

        public static string? GetUserRole(ClaimsPrincipal? user)
        {
            return user?.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "roles")
                ?.Value;
        }
    }
}
