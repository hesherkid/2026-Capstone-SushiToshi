namespace back_end.domain.enums;

public enum UserStatus
{
    Active,
    Inactive,
    Suspended,
    Deleted
}
// users.Role is stored as these numbers, so never change or reuse a number.
// Add new roles at the end with the next number.
public enum UserRoles
{
    Admin = 0,
    Employee = 1, // Not used: no policies or endpoints. Kept so the stored role numbers don't change.
    Customer = 2,
    Staff = 3
}

public enum ServiceRequestStatus
{
    Pending,
    Claimed,
    Completed,
    Cancelled
}

public enum OrderStatus
{
    Pending,
    Processing,
    Delivered,
    Cancelled
}

public enum MenuItemStatus
{
    Available,
    Unavailable,
    Seasonal
}
public enum BillStatus
{
    Open,
    Closed,
    Paid,
    Cancelled
}

public enum OverLimitBehavior
{
    Block,
    Charge
}

public enum ChargeReason
{
    None,
    OverLimit
}

public enum PricingDayType
{
    Weekday,
    Weekend,
    Holiday
}

public enum HolidaySource
{
    Manual,
    Imported
}

// Only Approved holidays change prices; imported ones start as Pending
public enum HolidayStatus
{
    Pending,
    Approved,
    Rejected
}
