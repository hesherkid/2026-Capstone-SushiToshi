"use client";
import { useCallback, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/hooks/useAuth";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { fetchMySessionId } from "@/services/tableSession";
import { TABLE_VISIT_ENDED_EVENT, TABLE_VISIT_EVENT, THANK_YOU_PATH } from "@/utils/tableVisit";
import storage from "@/utils/storage";

// Customers don't log out; the visit ends when staff close the table's session.
// Whichever customer page notices first (they all ask which session they're in), the guest
// is taken to the thank-you screen. Also checks on its own, for pages that don't refresh.
const VisitEndedWatcher = () => {
  const router = useRouter();
  const pathname = usePathname();
  const { userRole } = useAuth();
  const [isGuest, setIsGuest] = useState(false);

  useEffect(() => {
    const readGuest = () => setIsGuest(storage.get("guest") === "true");
    readGuest();
    window.addEventListener(TABLE_VISIT_EVENT, readGuest);
    return () => window.removeEventListener(TABLE_VISIT_EVENT, readGuest);
  }, [pathname]);

  const isCustomer = isGuest || userRole === "customer";

  useEffect(() => {
    if (!isCustomer) return undefined;
    const showThanks = () => router.replace(THANK_YOU_PATH);
    window.addEventListener(TABLE_VISIT_ENDED_EVENT, showThanks);
    return () => window.removeEventListener(TABLE_VISIT_ENDED_EVENT, showThanks);
  }, [isCustomer, router]);

  // Only while seated; fetchMySessionId fires the event above when the session is gone
  const check = useCallback(async () => {
    if (!storage.get("session_id")) return;
    await fetchMySessionId();
  }, []);

  useAutoRefresh(check, { enabled: isCustomer && pathname !== THANK_YOU_PATH });

  return null;
};

export default VisitEndedWatcher;
