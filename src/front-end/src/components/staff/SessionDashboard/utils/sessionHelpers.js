// The restaurant's clock. Times are shown in Edmonton time even if a device is set to another zone.
export const RESTAURANT_TIME_ZONE = "America/Edmonton";

/**
 * Parse API timestamps as UTC when no timezone is specified.
 * Assumes timezone-less datetime strings from the API are UTC.
 */
export const parseUtcDate = (value) => {
  if (!value) return null;

  const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value);

  const date = new Date(
    typeof value === "string" && !hasTimezone
      ? `${value}Z`
      : value
  );

  return Number.isNaN(date.getTime()) ? null : date;
};

/**
 * Format a date to a readable time string
 */
export const formatTime = (dateString) => {
  const date = parseUtcDate(dateString);
  if (!date) return "";
  return date.toLocaleTimeString("en-CA", {
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
    timeZone: RESTAURANT_TIME_ZONE,
  });
};

/**
 * Format UTC date to local date and time
 */
export const formatDateTime = (dateString) => {
  const date = parseUtcDate(dateString);
  if (!date) return "";
  return date.toLocaleString("en-CA", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
    timeZone: RESTAURANT_TIME_ZONE,
  });
};

/**
 * Format just the date
 */
export const formatDate = (dateString) => {
  try {
    const date = parseUtcDate(dateString);
    if (!date) return "";
    return date.toLocaleDateString("en-CA", {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      timeZone: RESTAURANT_TIME_ZONE,
    });
  } catch (error) {
    console.error("Error formatting date:", error);
    return "Invalid date";
  }
};

/**
 * Check if a session can be closed
 */
export const canCloseSession = (session) => {
  // Session can be closed if all bills are closed or cancelled
  return session.bills.every(
    (bill) => bill.status === "CLOSED" || bill.status === "CANCELLED"
  );
};

/**
 * Format bill name with guest count
 */
export const formatBillName = (bill) => {
  const total = bill.total_count || 0;
  return `${bill.bill_name} (${total} guest${total !== 1 ? 's' : ''})`;
};

/**
 * Get color for bill status chip
 */
export const getBillStatusColor = (status) => {
  if (!status) return "default";

  const normalizedStatus = status.toUpperCase();

  switch (normalizedStatus) {
    case "OPEN":
      return "success";
    case "CLOSED":
      return "default";
    case "CANCELLED":
      return "error";
    default:
      return "default";
  }
};

/**
 * 
 * get hex color value for bill status
 */
export const getStatusColorValue = (status) => {
  if (!status) return "#9e9e9e";

  const normalizedStatus = status.toUpperCase();

  switch (normalizedStatus) {
    case "OPEN":
      return "#4caf50";  // Green
    case "CLOSED":
      return "#9e9e9e";  // Gray
    case "CANCELLED":
      return "#f44336";  // Red
    default:
      return "#9e9e9e";
  }
};

/**
 * Format bill status for display
 */
export const formatBillStatus = (status) => {
  if (!status) return "Unknown";
  return status.charAt(0).toUpperCase() + status.slice(1).toLowerCase();
};

/**
 * Sort bills by status priority: OPEN > CLOSED > CANCELLED
 */
export const sortBillsByStatus = (bills) => {
  if (!Array.isArray(bills)) return [];

  const statusPriority = {
    OPEN: 1,
    CLOSED: 2,
    CANCELLED: 3,
  };

  return [...bills].sort((a, b) => {
    const aPriority = statusPriority[a.status?.toUpperCase()] ?? 999;
    const bPriority = statusPriority[b.status?.toUpperCase()] ?? 999;
    return aPriority - bPriority;
  });
};

/**
 * Format currency amount
 */
export const formatCurrency = (amount) => {
  return new Intl.NumberFormat("en-CA", {
    style: "currency",
    currency: "CAD",
  }).format(amount);
};

/**
 * Format pricing type for display
 */
export const formatPricingType = (pricingType) => {
  switch (pricingType) {
    case "Weekday":
      return "Weekday Pricing";
    case "Weekend":
      return "Weekend Pricing";
    case "Holiday":
      return "Holiday Pricing";
    default:
      return pricingType;
  }
};

/**
 * Check if a session has any pending bills
 */
export const hasOpenBills = (session) => {
  return session.bills.some((bill) => bill.status === "OPEN");
};

/**
 * Check if a session has reached maximum capacity
 */
export const isAtCapacity = (session, maxCapacity = 20) => {
  const totalGuests = session.bills.reduce(
    (sum, bill) => sum + (bill.total_count || 0),
    0
  );
  return totalGuests >= maxCapacity;
};

/**
 * Get table assignment description
 */
export const getTableDescription = (tableNumbers) => {
  if (!tableNumbers || tableNumbers.length === 0) return "No tables assigned";
  if (tableNumbers.length === 1) return `Table ${tableNumbers[0]}`;
  return `Tables ${tableNumbers.join(", ")}`;
};

/**
 * Check if a table can be removed from a session
 */
export const canRemoveTable = (session, tableNumber) => {
  // Can't remove tables if there are any bills
  if (session.bills && session.bills.length > 0) return false;

  // Can't remove the last table
  if (session.table_Numbers.length <= 1) return false;

  return true;
};

/**
 * Get session duration
 */
// Tables seated longer than this get a warning on the session dashboard
export const LONG_SEATING_MINUTES = 90;

/**
 * Whole minutes since a session started (or until it ended), or null if there's no start time
 */
export const getSessionMinutes = (startTime, endTime = null) => {
  const start = parseUtcDate(startTime);
  if (!start) return null;
  const end = endTime ? parseUtcDate(endTime) : new Date();
  // Never negative, even if the device clock is slightly behind the server
  return Math.floor(Math.max(0, end - start) / 1000 / 60);
};

export const isLongSeating = (startTime, endTime = null) =>
  (getSessionMinutes(startTime, endTime) ?? 0) > LONG_SEATING_MINUTES;

export const getSessionDuration = (startTime, endTime = null) => {
  const minutes = getSessionMinutes(startTime, endTime);
  if (minutes === null) return "—";
  const hours = Math.floor(minutes / 60);

  if (hours > 0) {
    return `${hours}h ${minutes % 60}m`;
  }
  return `${minutes}m`;
};

/**
 * Format guest breakdown for display
 */
export const formatGuestBreakdown = (bill) => {
  if (!bill) return "No guests";
  const parts = [];
  const adultCount =
    bill.adult_count ??
    bill.Adult_Count ??
    bill.adult_Count ??
    0;

  const seniorCount =
    bill.senior_count ??
    bill.Senior_Count ??
    bill.senior_Count ??
    0;

  const childCount =
    bill.child_count ??
    bill.Child_Count ??
    bill.child_Count ??
    0;

  const totCount =
    bill.tot_count ??
    bill.Tot_Count ??
    bill.tot_Count ??
    0;

  const totalCount =
    bill.total_count ??
    bill.Total_Guests ??
    bill.total_Guests ??
    0;

  if (adultCount > 0) {
    parts.push(`${adultCount} Adult${adultCount !== 1 ? 's' : ''}`);
  }
  if (seniorCount > 0) {
    parts.push(`${seniorCount} Senior${seniorCount !== 1 ? 's' : ''}`);
  }
  if (childCount > 0) {
    parts.push(`${childCount} Child${childCount !== 1 ? 'ren' : ''}`);
  }
  if (totCount > 0) {
    parts.push(`${totCount} Toddler${totCount !== 1 ? 's' : ''}`);
  }

  // If no breakdown available, show total count
  if (parts.length === 0) {
    return `${totalCount} guest${totalCount !== 1 ? 's' : ''}`;
  }

  return parts.join(", ");
};

/**
 * Validate bill data before creation
 */
export const validateBillData = (billData) => {
  const errors = {};

  if (!billData.billName?.trim()) {
    errors.billName = "Bill name is required";
  }

  const totalGuests =
    (Number(billData.adultCount) || 0) +
    (Number(billData.childCount) || 0) +
    (Number(billData.seniorCount) || 0) +
    (Number(billData.totCount) || 0);

  if (totalGuests <= 0) {
    errors.guests = "At least one guest is required";
  }

  return {
    isValid: Object.keys(errors).length === 0,
    errors,
  };
};
