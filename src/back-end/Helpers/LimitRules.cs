namespace back_end.Helpers
{
    public static class LimitRules
    {
        // 0 = unlimited
        public static int Cap(int limit) => limit == 0 ? int.MaxValue : limit;

        // Toddlers are a subset of children: 0 = inherit child limit
        public static int EffectToddler(int toddler, int child) => toddler == 0 ? child : toddler;

        public static string? Validate(int adult, int child, int senior, int toddler, int perOrder = 0)
        {
            if (adult < 0 || child < 0 || senior < 0 || toddler < 0 || perOrder < 0)
                return "Limits cannot be negative.";
            if (Cap(child) > Cap(adult)) return "Child limit cannot exceed Adult limit.";
            if (Cap(senior) > Cap(adult)) return "Senior limit cannot exceed Adult limit.";
            if (Cap(EffectToddler(toddler, child)) > Cap(child))
                return "Toddler limit cannot exceed Child limit.";
            return null; // per-order is independent of per-person limits
        }
    }
}