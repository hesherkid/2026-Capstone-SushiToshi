"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Tab, Tabs } from "@mui/material";

export const ANALYTICS_PAGES = [
  { href: "/analytics/analytic-page", label: "Overview" },
  { href: "/analytics/item-performance", label: "Item performance" },
  { href: "/analytics/browsing-behavior", label: "Browsing behavior" },
  { href: "/analytics/order-timing", label: "Order timing" },
  { href: "/analytics/table-turnover", label: "Table turnover" },
];

// Exact match first, then the longest matching parent (so /analytics/item-performance/x highlights its page).
const findCurrent = (pathname) => {
  if (!pathname) return false;
  const exact = ANALYTICS_PAGES.find((p) => p.href === pathname);
  if (exact) return exact.href;
  const parent = ANALYTICS_PAGES.filter((p) =>
    pathname.startsWith(`${p.href}/`),
  ).sort((a, b) => b.href.length - a.href.length)[0];
  return parent ? parent.href : false;
};

const AnalyticsNav = () => {
  const pathname = usePathname();

  return (
    <Tabs
      value={findCurrent(pathname)}
      role="navigation"
      aria-label="Analytics pages"
      variant="scrollable"
      scrollButtons="auto"
      allowScrollButtonsMobile
      sx={{ borderBottom: 1, borderColor: "divider", mb: 2 }}
    >
      {ANALYTICS_PAGES.map((page) => (
        <Tab
          key={page.href}
          label={page.label}
          value={page.href}
          component={Link}
          href={page.href}
        />
      ))}
    </Tabs>
  );
};

export default AnalyticsNav;
