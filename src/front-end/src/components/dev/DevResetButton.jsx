"use client";
import { ButtonBase } from "@mui/material";
import { NAV_HEIGHT } from "@/components/customer/CustomerBottomNav";

export const DEV_RESET_PATH = "/auth/login";

// Forgets everything the app keeps in this browser (sign-in, guest, table visit, cart),
// then a full page load to sign-in so nothing in memory survives either
export const resetBrowserSession = () => {
  try {
    localStorage.clear();
    sessionStorage.clear();
  } catch {
    // Storage blocked: the reload still starts over
  }
  window.location.href = DEV_RESET_PATH;
};

/**
 * Development only: one click back to a clean sign-in page, for switching between
 * customer, guest, staff and admin while testing. Customers can't log out while seated,
 * guests never can, and the thank-you page holds the back button, so without this it's
 * easy to get stuck. `next build` sets NODE_ENV to "production", so it never ships.
 */
const DevResetButton = () => {
  if (process.env.NODE_ENV !== "development") return null;

  return (
    <ButtonBase
      onClick={resetBrowserSession}
      aria-label="Dev: reset and sign out"
      title="Development only: clears this browser's sign-in, table visit and cart, then opens sign-in"
      sx={{
        position: "fixed",
        left: 12,
        // Above the phone tab bar
        bottom: { xs: `calc(${NAV_HEIGHT + 12}px + env(safe-area-inset-bottom))`, md: 12 },
        zIndex: (theme) => theme.zIndex.modal + 1,
        px: 1.25,
        height: 30,
        borderRadius: 999,
        border: "1px dashed",
        borderColor: "warning.main",
        bgcolor: "rgba(255, 248, 225, 0.95)",
        color: "warning.dark",
        fontSize: "0.75rem",
        fontWeight: 700,
        letterSpacing: "0.02em",
        boxShadow: 1,
        "&:hover": { bgcolor: "warning.light", color: "common.black" },
      }}
    >
      DEV · Reset
    </ButtonBase>
  );
};

export default DevResetButton;
