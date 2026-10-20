using back_end.domain.enums;

namespace back_end.Helpers
{
    /// <summary>
    /// Order-time limit rules.
    ///
    /// Two scopes, category and item limits both apply:
    ///   - Per-order: the whole table, reset for every order. Always blocks.
    ///   - Per-person: per dining session; cap = limit x headcount per age group.
    ///     Over the cap the binding limit's behavior decides: Block rejects, Charge splits
    ///     the excess into a line charged at Full_Price (Charge_Reason = OverLimit).
    /// 0 = unlimited; toddler 0 = same as child (see LimitRules).
    /// </summary>

    public static class OrderLimitRules
    {
        public sealed record Headcount(int Adults, int Children, int Seniors, int Toddlers)
        {
            /// <summary>
            /// Used when a session has no bills yet, so limits still apply to at least one guest.
            /// </summary>
            public static readonly Headcount Fallback = new(1, 0, 0, 0);
            public bool IsEmpty => Adults + Children + Seniors + Toddlers == 0;
        }

        public sealed record Limits(int Adult, int Child, int Senior, int Toddler, int PerOrder, OverLimitBehavior Behavior);

        /// <summary>
        /// Units already ordered, used to work out what is left.
        /// </summary>
        public sealed record Usage(int OrderCategory, int OrderItem, int SessionCategory, int SessionItem);

        public sealed record LimitLine(int Quantity, decimal Price, ChargeReason Reason);

        public sealed record LimitResult(IReadOnlyList<LimitLine> Lines, string? Error)
        {
            public bool IsValid => Error is null;
        }

        /// <summary>
        /// Per-person cap for a dining session.
        /// </summary>
        public static int SessionCap(Limits limits, Headcount heads)
        {
            var groups = new (int Limit, int Count)[]
            {
                (limits.Adult, heads.Adults),
                (limits.Child, heads.Children),
                (limits.Senior, heads.Seniors),
                (LimitRules.EffectToddler(limits.Toddler, limits.Child), heads.Toddlers)
            };

            long total = 0;
            foreach (var (limit, count) in groups)
            {
                if (count == 0) continue;
                if (limit == 0) return int.MaxValue;   // someone at the table is unlimited
                total += (long)limit * count;
            }
            return (int)Math.Min(total, int.MaxValue);
        }

        /// <summary>
        /// Per-order cap that always blocks, or null when there is none.
        /// </summary>
        public static int? PerOrderCap(Limits limits) => limits.PerOrder == 0 ? null : limits.PerOrder;

        /// <summary>
        /// Units the table can still order this session before a Block limit stops them, or null
        /// when nothing blocks. Charge limits never block (the extra is billed at Full_Price).
        /// </summary>
        public static int? SessionLeftBeforeBlock(Limits limits, Headcount heads, int sessionUsed)
        {
            if (limits.Behavior != OverLimitBehavior.Block) return null;
            var cap = SessionCap(limits, heads);
            return cap == int.MaxValue ? null : Math.Max(0, cap - sessionUsed);
        }

        /// <summary>
        /// Works out the order lines for one requested item, or an error if a Block limit is exceeded.
        /// </summary>
        public static LimitResult ApplyLimits(
            string itemName,
            string categoryName,
            int requested,
            Limits category,
            Limits item,
            Headcount heads,
            Usage used,
            bool isAddOn,
            decimal price,
            decimal fullPrice
        )
        {
            // Per-order limits = always block
            var itemOrderLeft = LimitRules.Cap(item.PerOrder) - used.OrderItem;
            if (requested > itemOrderLeft)
                return Fail($"{itemName}: maximum {item.PerOrder} per order ({Math.Max(0, itemOrderLeft)} left in this order).");

            var categoryOrderLeft = LimitRules.Cap(category.PerOrder) - used.OrderCategory;
            if (requested > categoryOrderLeft)
                return Fail($"{categoryName}: maximum {category.PerOrder} combined per order ({Math.Max(0, categoryOrderLeft)} left in this order).");

            // Per-person limits for the dining session - both apply, tighter one binds
            var categoryCap = SessionCap(category, heads);
            var itemCap = SessionCap(item, heads);
            var categoryLeft = categoryCap == int.MaxValue ? int.MaxValue : categoryCap - used.SessionCategory;
            var itemLeft = itemCap == int.MaxValue ? int.MaxValue : itemCap - used.SessionItem;

            var left = Math.Max(0, Math.Min(categoryLeft, itemLeft));
            var freeQty = Math.Min(requested, left);
            var overQty = requested - freeQty;

            var lines = new List<LimitLine>();
            var basePrice = isAddOn ? price : 0m;
            if (freeQty > 0) lines.Add(new LimitLine(freeQty, basePrice, ChargeReason.None));

            if (overQty > 0)
            {
                var categoryBinds = categoryLeft <= itemLeft;
                var binding = categoryBinds ? category : item;
                var scope = categoryBinds ? categoryName : itemName;
                var cap = categoryBinds ? categoryCap : itemCap;

                if (binding.Behavior == OverLimitBehavior.Block)
                    return Fail($"{scope}: limit for this table is {cap} per dining session ({left} left).");

                if (fullPrice <= 0)
                    return Fail($"{itemName}: over the limit but no full price is set. Ask staff for help.");

                lines.Add(new LimitLine(overQty, fullPrice, ChargeReason.OverLimit));
            }

            return new LimitResult(lines, null);
        }

        private static LimitResult Fail(string message) => new(Array.Empty<LimitLine>(), message);
    }
}