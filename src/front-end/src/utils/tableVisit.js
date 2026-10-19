// The table a customer is sitting at, from the table's QR code, kept in localStorage.

// Fired when the QR table changes so the app bar's "Table X" chip updates without a page change
export const TABLE_VISIT_EVENT = "table-visit-changed";

// Belong to one visit; a new scan must not reuse them
const VISIT_KEYS = ["session_id", "currentBillId", "menuCart"];

/**
 * Called as soon as a table QR code is opened, before sign-in. Saves the scanned table and
 * forgets the previous visit's session and bill, so a refused join can't leave the menu
 * ordering onto another table's session. Joining sets session_id again.
 */
export const startTableVisit = ({ locationId, tableNumber }) => {
  if (typeof window === "undefined" || !locationId || !tableNumber) return;

  localStorage.setItem("locationId", String(locationId));
  localStorage.setItem("tableNumber", String(tableNumber));
  VISIT_KEYS.forEach((key) => localStorage.removeItem(key));

  window.dispatchEvent(new Event(TABLE_VISIT_EVENT));
};
