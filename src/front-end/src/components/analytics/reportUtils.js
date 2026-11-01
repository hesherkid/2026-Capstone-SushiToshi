import { useSyncExternalStore } from "react";
import storage from "@/utils/storage";
import { isHtmlPage } from "@/utils/serverMessage";

const RANGE_NOTES = {
  today: "today so far",
  yesterday: "yesterday",
  last7: "the last 7 full days",
  last30: "the last 30 full days",
  custom: "both dates included",
};

const parseLocalDate = (value) => {
  const [y, m, d] = value.split("-").map(Number);
  return new Date(y, m - 1, d);
};

const formatDate = (value) =>
  parseLocalDate(value).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
  });

export const toInputDate = (date) => {
  const pad = (n) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
};

export const describeRange = (range) => {
  if (!range) return "";
  const span =
    range.from === range.to
      ? formatDate(range.from)
      : `${formatDate(range.from)} - ${formatDate(range.to)}`;
  return `${span} (${RANGE_NOTES[range.preset] ?? range.preset}), ${range.time_zone} time`;
};

// The API sends null when there's nothing to calculate (e.g. no views), shown as a dash.
export const pct = (value) =>
  value === null || value === undefined ? "-" : `${value}%`;
export const secs = (value) =>
  value === null || value === undefined ? "-" : `${value}s`;

// Handles { message }, ASP.NET ValidationProblem { errors }, plain strings and network failures.
export const getErrorMessage = (err, fallback) => {
  if (!err.response) {
    return "Couldn't reach the server. Check your connection and try again.";
  }
  const { status, data } = err.response;
  if (status === 401)
    return "Your session has expired. Log in again to continue.";
  if (data?.errors) return Object.values(data.errors).flat().join(" ");
  if (typeof data === "string" && data && !isHtmlPage(data)) return data;
  return data?.message || data?.title || fallback;
};

// Hydration-safe browser reads: the server render and the first client render both see the
// "server" value, then React switches to the real value. Avoids a server/client markup mismatch.
const noopSubscribe = () => () => {};
const subscribeToStorage = (callback) => {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
};

export const useIsClient = () =>
  useSyncExternalStore(
    noopSubscribe,
    () => true,
    () => false,
  );

// The location saved on this device, or null.
export const useHomeLocation = () =>
  useSyncExternalStore(
    subscribeToStorage,
    () => storage.get("branch-location"),
    () => null,
  );
