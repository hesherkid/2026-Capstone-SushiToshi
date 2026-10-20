"use client";
import { useState, useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { BottomNavigation, BottomNavigationAction, Box, Paper } from "@mui/material";
import { useAuth } from "@/hooks/useAuth";
import { TABLE_VISIT_EVENT } from "@/utils/tableVisit";
import { CUSTOMER_NAV_ITEMS } from "@/components/customer/customerNavItems";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import { REVIEW_ORDER_PATH } from "@/contexts/TableOrderContext";

export const NAV_HEIGHT = 64;
const INACTIVE_COLOR = "#6b6f73";

// Phone tab bar for guests: every customer page is one tap away.
const CustomerBottomNav = () => {
  const pathname = usePathname();
  const router = useRouter();
  const { userRole } = useAuth();
  const [isGuest, setIsGuest] = useState(false);

  // Same signal the app bar uses: the guest flag is saved at QR login
  useEffect(() => {
    const readGuest = () => setIsGuest(localStorage.getItem("guest") === "true");
    readGuest();
    window.addEventListener(TABLE_VISIT_EVENT, readGuest);
    return () => window.removeEventListener(TABLE_VISIT_EVENT, readGuest);
  }, [pathname]);

  const isCustomer = isGuest || userRole === "customer";
  // Review order has its own Place order bar at the bottom, so the tabs step aside there
  if (!isCustomer || pathname.startsWith("/auth") || pathname.startsWith(REVIEW_ORDER_PATH)) {
    return null;
  }

  const current = CUSTOMER_NAV_ITEMS.find((item) => pathname.startsWith(item.path))?.path ?? false;

  return (
    <Box sx={{ display: { xs: "block", md: "none" } }}>
      {/* Keeps the end of the page (and the footer) from hiding under the bar */}
      <Box sx={{ height: `calc(${NAV_HEIGHT}px + env(safe-area-inset-bottom))` }} />

      <Paper
        component="nav"
        aria-label="Customer navigation"
        elevation={0}
        square
        sx={{
          position: "fixed",
          left: 0,
          right: 0,
          bottom: 0,
          zIndex: (theme) => theme.zIndex.appBar,
          pb: "env(safe-area-inset-bottom)",
          backgroundColor: "background.paper",
          borderTop: "1px solid rgba(0, 0, 0, 0.08)",
          boxShadow: "0 -2px 12px rgba(0, 0, 0, 0.06)",
        }}
      >
        <BottomNavigation
          showLabels
          value={current}
          onChange={(_, path) => router.push(path)}
          sx={{
            height: NAV_HEIGHT,
            backgroundColor: "transparent",
            "& .MuiBottomNavigationAction-root": {
              minWidth: 0,
              px: 0.5,
              pt: 1,
              gap: 0.5,
              color: INACTIVE_COLOR,
              transition: "color 150ms ease",
              "&.Mui-selected": { color: "primary.main" },
            },
            // Same size selected or not, so labels don't jump; only the weight changes
            "& .MuiBottomNavigationAction-label, & .MuiBottomNavigationAction-label.Mui-selected": {
              fontSize: "0.72rem",
              fontWeight: 500,
              letterSpacing: "0.01em",
              whiteSpace: "nowrap",
            },
            "& .MuiBottomNavigationAction-label.Mui-selected": { fontWeight: 700 },
          }}
        >
          {CUSTOMER_NAV_ITEMS.map(({ label, path, symbol }) => (
            <BottomNavigationAction
              key={path}
              disableRipple
              value={path}
              label={label}
              icon={<MaterialSymbol name={symbol} filled={current === path} size={30} />}
            />
          ))}
        </BottomNavigation>
      </Paper>
    </Box>
  );
};

export default CustomerBottomNav;
