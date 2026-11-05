using Google.Apis.Auth;

namespace back_end.Helpers
{
    // Google's ID token may have no name at all, but First_name and Last_name can't be null
    public static class GoogleNames
    {
        // Same as the MaxLength on User.First_name and User.Last_name
        public const int MaxLength = 100;

        // Uses the given and family names when Google sends them, else the full name as the first
        // name; any part that's missing is empty
        public static (string First, string Last) FromPayload(GoogleJsonWebSignature.Payload payload)
        {
            var first = payload.GivenName?.Trim() ?? "";
            var last = payload.FamilyName?.Trim() ?? "";

            if (first.Length == 0 && last.Length == 0)
            {
                first = payload.Name?.Trim() ?? "";
            }

            return (Fit(first), Fit(last));
        }

        private static string Fit(string name) =>
            name.Length > MaxLength ? name[..MaxLength].TrimEnd() : name;
    }
}
