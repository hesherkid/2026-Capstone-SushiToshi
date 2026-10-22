"use client";
import { useRouter } from "next/navigation";
import { BottomNavigation, BottomNavigationAction, Box, Paper } from "@mui/material";
import { CUSTOMER_NAV_ITEMS } from "@/components/customer/customerNavItems";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import useCustomerNav from "@/components/customer/useCustomerNav";

export const NAV_HEIGHT = 64;
export const INACTIVE_COLOR = "#6b6f73";

// Phone tab bar for guests: every customer page is one tap away.
const CustomerBottomNav = () => {
  const router = useRouter();
  const { show, current } = useCustomerNav();

  if (!show) return null;

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
