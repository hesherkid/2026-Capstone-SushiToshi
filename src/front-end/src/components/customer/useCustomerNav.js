import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { useAuth } from "@/hooks/useAuth";
import { TABLE_VISIT_EVENT, THANK_YOU_PATH, hasTableVisit } from "@/utils/tableVisit";
import { CUSTOMER_NAV_ITEMS } from "@/components/customer/customerNavItems";
import { REVIEW_ORDER_PATH } from "@/contexts/TableOrderContext";

/**
 * When the customer pages' navigation shows (bottom tabs on phones, side rail on desktop),
 * and which page is current. Both use this so they always appear and hide together.
 */
const useCustomerNav = () => {
  const pathname = usePathname();
  const { userRole } = useAuth();
  const [atTable, setAtTable] = useState(false);

  // The session is saved when the table is joined. Read again on each page, since joining
  // happens on the way in
  useEffect(() => {
    const readVisit = () => setAtTable(hasTableVisit());
    readVisit();
    window.addEventListener(TABLE_VISIT_EVENT, readVisit);
    return () => window.removeEventListener(TABLE_VISIT_EVENT, readVisit);
  }, [pathname]);

  // Guests' accounts are customers too
  const isCustomer = userRole === "customer";
  // Without a table (not joined yet, or the visit ended) there's nothing to go to.
  // Review order has its own Place order bar at the bottom, so the tabs step aside there
  const show =
    isCustomer &&
    atTable &&
    !pathname.startsWith("/auth") &&
    !pathname.startsWith(REVIEW_ORDER_PATH) &&
    !pathname.startsWith(THANK_YOU_PATH);

  const current = CUSTOMER_NAV_ITEMS.find((item) => pathname.startsWith(item.path))?.path ?? false;

  return { show, current, isCustomer };
};

export default useCustomerNav;
