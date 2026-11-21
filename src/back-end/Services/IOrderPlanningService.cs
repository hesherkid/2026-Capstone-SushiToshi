using back_end.domain.Entities;
using back_end.Dtos;

namespace back_end.Services
{
    // The priced, limit-checked lines for an order request.
    // If Errors has anything in it, none of the lines should be saved (all or nothing).
    public sealed record OrderLinePlan(List<OrderItems> Lines, Dictionary<int, string> NamesById, List<string> Errors);

    // Works out what a bill can order: prices requested items and applies the ordering limits.
    // Nothing here saves to the database; the caller decides what to store.
    public interface IOrderPlanningService
    {
        // Prices the requested items and applies the limits, without saving anything.
        // orderId is the order being added to, or null for a new order.
        Task<OrderLinePlan> PlanOrderLinesAsync(DiningSession session, int billId, int? orderId, List<OrderItemCreateDto> items);

        // How much of each item and category the bill (or the whole table when billId is null)
        // can still order before a blocking limit rejects it
        Task<SessionOrderLimitsDto> GetRemainingLimitsAsync(DiningSession session, int? billId);
    }
}
