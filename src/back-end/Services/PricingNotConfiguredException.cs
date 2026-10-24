namespace back_end.Services
{
    // Thrown instead of guessing a price; replaces the old hard-coded fallbacks (e.g. 0.13 tax)
    public class PricingNotConfiguredException : Exception
    {
        public PricingNotConfiguredException(string message) : base(message) { }
    }
}
