namespace back_end.Helpers
{
    public static class ViewTrackingRules
    {
        public const int MinQualifyingSeconds = 5;

        public const int MaxRecordedSeconds = 600;

        public const string RateLimitPolicy = "views";

        public const int RateLimitPerMinute = 60;

        public const int DefaultMinViews = 10;

        public const int MaxMinViews = 1000;
    }
}

