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

// Fired when staff close the table's session; customers are shown the thank-you screen
export const TABLE_VISIT_ENDED_EVENT = "table-visit-ended";

export const THANK_YOU_PATH = "/thank-you";

/**
 * Called when the server no longer has this customer at the table they were seated at,
 * i.e. staff ended the session. Forgets the table, session, bill and cart; the guest
 * account is kept so the next scan reuses it.
 */
export const endTableVisit = () => {
  if (typeof window === "undefined") return;

  [...VISIT_KEYS, "tableNumber", "locationId", "pendingOrder"].forEach((key) =>
    localStorage.removeItem(key),
  );

  window.dispatchEvent(new Event(TABLE_VISIT_EVENT));
  window.dispatchEvent(new Event(TABLE_VISIT_ENDED_EVENT));
};
