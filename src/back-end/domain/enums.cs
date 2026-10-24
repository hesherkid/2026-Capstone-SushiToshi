namespace back_end.domain.enums;

public enum UserStatus
{
    Active,
    Inactive,
    Suspended,
    Deleted
}
public enum UserRoles
{
    Admin,
    Employee,
    Customer,
    Staff
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
