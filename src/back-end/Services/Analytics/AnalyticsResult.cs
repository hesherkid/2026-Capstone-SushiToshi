namespace back_end.Services.Analytics
{
    public enum AnalyticsResultStatus
    {
        Ok,
        Invalid,
        NotFound
    }

    /// <summary>
    /// Outcome of an analytics report. Services return this instead of IActionResult so they
    /// stay free of HTTP concerns; controllers turn it into 200 / 400 / 404.
    /// Unexpected failures are thrown, not returned, and the controller maps them to 500.
    /// </summary>
    public sealed class AnalyticsResult<T>
    {
        private AnalyticsResult(AnalyticsResultStatus status, T? value, string errorKey, string errorMessage)
        {
            Status = status;
            Value = value;
            ErrorKey = errorKey;
            ErrorMessage = errorMessage;
        }

        public AnalyticsResultStatus Status { get; }

        public T? Value { get; }

        public string ErrorKey { get; }

        public string ErrorMessage { get; }

        public bool IsOk => Status == AnalyticsResultStatus.Ok;

        public static AnalyticsResult<T> Ok(T value) =>
            new(AnalyticsResultStatus.Ok, value, string.Empty, string.Empty);

        public static AnalyticsResult<T> Invalid(string errorKey, string errorMessage) =>
            new(AnalyticsResultStatus.Invalid, default, errorKey, errorMessage);

        public static AnalyticsResult<T> NotFound(string errorMessage) =>
            new(AnalyticsResultStatus.NotFound, default, string.Empty, errorMessage);

        public AnalyticsResult<TOther> AsError<TOther>()
        {
            if (IsOk)
            {
                throw new InvalidOperationException("Only a failed result can be passed on as an error.");
            }

            return new AnalyticsResult<TOther>(Status, default, ErrorKey, ErrorMessage);
        }
    }
}