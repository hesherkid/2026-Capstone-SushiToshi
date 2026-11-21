import { useState, useCallback, useRef } from "react";
import api from "@/config/api";
import storage from "@/utils/storage";

// "2026-11" for any date inside November 2026
export const toMonthKey = (date) => {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  return `${y}-${m}`;
};

// Which days have orders, loaded one month at a time as staff page through the calendar.
// Past months are cached; the current month is re-checked on each visit since orders keep coming in
export const useOrderDays = () => {
  const [orderDays, setOrderDays] = useState(() => new Set());
  const [loadingMonth, setLoadingMonth] = useState(null);
  const loaded = useRef(new Set());

  const loadMonth = useCallback(async (date) => {
    const month = toMonthKey(date);
    const isCurrentMonth = month === toMonthKey(new Date());
    const seen = loaded.current.has(month);
    if (seen && !isCurrentMonth) return;

    loaded.current.add(month);
    // Only show the spinner the first time; a background refresh keeps the dots on screen
    if (!seen) setLoadingMonth(month);
    try {
      const params = { month };
      const locationId = storage.get("branch-location");
      if (locationId) params.location_id = locationId;

      const response = await api.get("/Order/history/days", { params });
      const days = Array.isArray(response.data) ? response.data : [];
      setOrderDays((prev) => {
        const next = new Set(
          [...prev].filter((day) => !day.startsWith(`${month}-`)),
        );
        days.forEach((day) => next.add(day));
        return next;
      });
    } catch (err) {
      // The marks are a nice-to-have: leave the calendar usable and try again next time
      console.error("Error fetching days with orders:", err);
      loaded.current.delete(month);
    } finally {
      setLoadingMonth((current) => (current === month ? null : current));
    }
  }, []);

  return { orderDays, loadMonth, isLoading: loadingMonth !== null };
};
