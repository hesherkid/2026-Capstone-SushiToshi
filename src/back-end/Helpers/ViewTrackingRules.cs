namespace back_end.Helpers
{
    /// <summary>
    /// Shared rules for recording and reporting menu item views.
    /// </summary>
    public static class ViewTrackingRules
    {
        /// <summary>Views shorter than this are stored but ignored by reports (a glance, not a read).</summary>
        public const int MinQualifyingSeconds = 5;

        /// <summary>
        /// Longest view stored. A phone left open on an item would otherwise inflate averages.
        /// Longer views are capped at this value, not rejected.
        /// </summary>
        public const int MaxRecordedSeconds = 600;

        /// <summary>Rate limiter policy name used by POST api/menu-item-views.</summary>
        public const string RateLimitPolicy = "views";

        /// <summary>Views accepted per minute per signed-in token.</summary>
        public const int RateLimitPerMinute = 60;
    }
}