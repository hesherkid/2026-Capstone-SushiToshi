"use client";

import useItemViewTracker from "@/hooks/useItemViewTracker";

/* Wraps one menu item card and records how long it's on screen (see useItemViewTracker).*/
const TrackedItem = ({ itemId, sessionId, children }) => {
  const ref = useItemViewTracker(itemId, sessionId);
  return (
    <div ref={ref} style={{ width: "100%" }}>
      {children}
    </div>
  );
};

export default TrackedItem;
