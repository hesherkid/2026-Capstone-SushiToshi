"use client";

import { useEffect, useRef, useState } from "react";
import { API_BASE_URL } from "@/config/api";

// How much of the card needs to be on screen to count as "looking at it"
const VISIBLE_RATIO = 0.6;

// Shorter views aren't sent at all. Reports only count view of 5+ seconds
// Avoids a request for every card scrolled past.
const MIN_SEND_SECONDS = 1;

/**
 * Records one item view. Uses fetch with keepalive so the request still completes if
 * the guest closes the tab. Never throws: analytics must not break the menu.
 */

export const sendItemView = ({ sessionId, itemId, seconds }) => {
  const token = localStorage.getItem("authToken");
  if (!token) return;

  fetch(`${API_BASE_URL}/menu-item-views`, {
    method: "POST",
    keepalive: true,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({
      session_id: sessionId,
      item_id: itemId,
      view_seconds: seconds,
    }),
  }).catch(() => {});
};

/**
 * Times how long a menu item card is on screen and records it as a view.
 * A view starts when at least 60% of the card is on screen and ends when it scrolls away,
 * the category closes, the guest switches app or locks the phone, or the page closes.
 *
 * @param {number} itemId     the item on the card
 * @param {number} sessionId  the dining session (the menu page already has it)
 */
export default function useItemViewTracker(itemId, sessionId) {
  const ref = useRef(null);
  const [onScreen, setOnScreen] = useState(false);

  // Watch whether the card is on screen.
  useEffect(() => {
    const element = ref.current;
    if (!element || typeof IntersectionObserver === "undefined")
      return undefined;

    const observer = new IntersectionObserver(
      ([entry]) =>
        setOnScreen(
          entry.isIntersecting && entry.intersectionRatio >= VISIBLE_RATIO,
        ),
      { threshold: [0, VISIBLE_RATIO] },
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  // While it's on screen, time the view and send it when the view ends.
  useEffect(() => {
    if (!onScreen || !itemId || !(sessionId > 0)) return undefined;

    const startedAt = performance.now();
    let finished = false;

    const finish = () => {
      if (finished) return;
      finished = true;
      const seconds = Math.round((performance.now() - startedAt) / 1000);
      if (seconds >= MIN_SEND_SECONDS) {
        sendItemView({ sessionId, itemId, seconds });
      }
    };
    const handleVisibilityChange = () => {
      if (document.visibilityState === "hidden") finish();
    };

    document.addEventListener("visibilitychange", handleVisibilityChange);
    window.addEventListener("pagehide", finish);

    return () => {
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      window.removeEventListener("pagehide", finish);
      finish();
    };
  }, [onScreen, itemId, sessionId]);

  return ref;
}
