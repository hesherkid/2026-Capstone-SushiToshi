import { useEffect, useRef } from "react";

// How often customer pages re-check their data (orders, bills, limits)
export const AUTO_REFRESH_MS = 15000;

/**
 * Calls `refresh` every `intervalMs` while the page is on screen, and right away when the
 * guest comes back to it (phone unlocked, tab switched back). Nothing runs while it's hidden.
 * The page loads its own data on arrival; this only keeps it current afterwards.
 * Pass enabled = false to pause it (e.g. while an order is being sent).
 */
export const useAutoRefresh = (refresh, { intervalMs = AUTO_REFRESH_MS, enabled = true } = {}) => {
  const latest = useRef(refresh);
  // A slow refresh isn't started again until it finishes
  const running = useRef(false);

  useEffect(() => {
    latest.current = refresh;
  }, [refresh]);

  useEffect(() => {
    if (!enabled) return undefined;

    const run = async () => {
      if (running.current) return;
      running.current = true;
      try {
        await latest.current();
      } catch (err) {
        console.error("Auto refresh failed:", err);
      } finally {
        running.current = false;
      }
    };

    let timer = null;
    const stop = () => {
      clearInterval(timer);
      timer = null;
    };
    const start = () => {
      stop();
      timer = setInterval(run, intervalMs);
    };

    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") {
        run();
        start();
      } else {
        stop();
      }
    };

    if (document.visibilityState === "visible") start();
    document.addEventListener("visibilitychange", onVisibilityChange);

    return () => {
      stop();
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [enabled, intervalMs]);
};

export default useAutoRefresh;
