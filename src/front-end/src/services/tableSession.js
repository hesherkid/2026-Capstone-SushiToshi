import api from "@/config/api";
import { decodeToken } from "@/config/auth";
import { endTableVisit } from "@/utils/tableVisit";

const NAME_IDENTIFIER =
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";

// The signed-in user's account ID from their token (guests included), or null
export const getMyUserId = () => {
  if (typeof window === "undefined") return null;
  const token = localStorage.getItem("authToken");
  const claims = token ? decodeToken(token) : null;
  const id = Number(claims?.[NAME_IDENTIFIER] ?? claims?.nameid);
  return id > 0 ? id : null;
};

/**
 * Asks the server which dining session the signed-in customer is seated in. Works for guests
 * too, since every guest has their own account. Returns the session ID, or null if they
 * aren't seated at an active table.
 *
 * Customer pages use this instead of trusting a session_id saved in the browser: a leftover
 * value (another tab, an earlier visit) pointed them at another table's bills.
 * The answer is saved as session_id for code that still reads it from localStorage.
 *
 * If this phone was seated and the server now has no session for it, staff ended the
 * session: the visit is cleared and the thank-you screen is shown (see endTableVisit).
 */
export const fetchMySessionId = async () => {
  const wasSeated = !!localStorage.getItem("session_id");

  try {
    const { data } = await api.get(
      "/DiningSession/participants/active-session-id",
    );
    const sessionId = Number(data?.session_id);
    if (sessionId > 0) {
      localStorage.setItem("session_id", String(sessionId));
      return sessionId;
    }
  } catch (err) {
    // 404 means not seated anywhere; anything else is a real failure for the caller to show
    if (err.response?.status !== 404) throw err;
  }

  localStorage.removeItem("session_id");
  if (wasSeated) endTableVisit();
  return null;
};

// A 403 on the table's bills, orders or requests: this person isn't seated there any more.
// Staff closed the table, or they moved
export const isSeatGone = (err) => err?.response?.status === 403;

/**
 * Asks the server again after isSeatGone: fetchMySessionId shows the thank-you screen if they're
 * no longer seated anywhere. Returns the session they're seated at now, or null.
 * The Bill page and its bill details poll separately, so calls made at the same time share
 * one check (one thank-you screen, not two).
 */
let recheckInFlight = null;

export const recheckSeat = () => {
  recheckInFlight ??= fetchMySessionId()
    .catch((err) => {
      console.error("Error re-checking the table:", err);
      return null;
    })
    .finally(() => {
      recheckInFlight = null;
    });
  return recheckInFlight;
};

export const NOT_SEATED_MESSAGE =
  "You're not seated at a table yet. Scan the QR code on your table, or ask staff to start your table.";
