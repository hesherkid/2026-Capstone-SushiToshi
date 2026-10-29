"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Box, Button, ButtonBase, Paper, Typography } from "@mui/material";
import { decodeToken, isTokenExpired } from "@/config/auth";
import { getAuthToken, isGuestClaims, logoutCustomer } from "@/utils/auth";
import { hasTableVisit } from "@/utils/tableVisit";
import useAuth from "@/hooks/useAuth";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { fetchMySessionId } from "@/services/tableSession";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import MaterialSymbol from "@/components/common/MaterialSymbol";

const MENU_PATH = "/menu/full-menu";
const LOGIN_PATH = "/auth/login";
const VISITS_PATH = "/visits";
// Log out waits on the visit ending, so this page checks sooner than the 15 s watcher
const SEATED_CHECK_MS = 5000;

const STEPS = [
  { symbol: "qr_code_scanner", title: "Scan your table's QR code", text: "It's on the table, and joins you to your table's order." },
  { symbol: "menu_book_2", title: "Order from the menu", text: "Order as many rounds as you like. We'll bring them to your table." },
  { symbol: "receipt_long", title: "Ask for your bill", text: "Tap Ready for bill when you're done, and your server will bring it." },
];

const isGuestAccount = () => {
  if (typeof window === "undefined") return false;
  if (localStorage.getItem("guest") === "true") return true;

  const token = getAuthToken();
  if (!token || isTokenExpired(token)) return false;
  return isGuestClaims(decodeToken(token));
};

// Saved from the sign-in response; the token itself carries no name
const savedFirstName = () => {
  try {
    return JSON.parse(localStorage.getItem("user") || "null")?.first_name || "";
  } catch {
    return "";
  }
};

/**
 * Home page for registered customers: how to get started (scan the QR code). Guests go
 * straight to the menu, and signed-out visitors to sign in.
 */
const CustomerHome = () => {
  const router = useRouter();
  // Every useAuth call starts signed out and loading; only its answer after loading counts
  const { isAuthenticated, loading: authLoading } = useAuth();
  // Whether to show the page yet: guests and signed-out visitors are sent elsewhere
  const [ready, setReady] = useState(false);
  const [firstName, setFirstName] = useState("");
  // Seated at a table from this phone: no logging out until the visit ends
  const [seated, setSeated] = useState(false);
  const [tableNumber, setTableNumber] = useState("");

  // Only guests skip this page. A registered customer joins a table by scanning its QR code,
  // never just by signing in, so they always get the steps here. It's their page, so
  // signing in comes first
  useEffect(() => {
    if (authLoading) return;
    if (isGuestAccount()) {
      router.replace(MENU_PATH);
      return;
    }
    if (!isAuthenticated) {
      router.replace(LOGIN_PATH);
      return;
    }
    setFirstName(savedFirstName());
    setSeated(hasTableVisit());
    setTableNumber(localStorage.getItem("tableNumber") || "");
    setReady(true);
  }, [authLoading, isAuthenticated, router]);

  // fetchMySessionId ends the visit (and shows the thank-you screen, with Log out) once staff
  // have closed the table. Checked on arrival too, in case it closed while they were away
  // A failed check just waits for the next one
  const checkSeated = async () => {
    try {
      await fetchMySessionId();
    } catch (err) {
      console.error("Error checking table visit:", err);
    }
  };

  useEffect(() => {
    if (ready && seated) checkSeated();
  }, [ready, seated]);

  useAutoRefresh(checkSeated, { intervalMs: SEATED_CHECK_MS, enabled: ready && seated });

  if (!ready) return null;

  return (
    <CustomerPageContainer sx={{ maxWidth: 560 }}>
      {/* ---- Welcome ---- */}
      <Box sx={{ textAlign: "center", pt: { xs: 5, sm: 6 }, pb: 3 }}>

        <Typography variant="h4" component="h1" sx={{ mt: 2.5, fontWeight: 700 }}>
          {firstName ? `Welcome back, ${firstName}!` : "Welcome back!"}
        </Typography>
      </Box>

      {/* ---- How it works ---- */}
      <Paper variant="outlined" sx={{ borderRadius: 3, overflow: "hidden", textAlign: "left" }}>
        <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 700, px: 2.5, pt: 2, pb: 0.5 }}>
          How it works
        </Typography>
        <Box component="ol" sx={{ listStyle: "none", p: 0, m: 0 }}>
          {STEPS.map(({ symbol, title, text }, index) => (
            <Box
              component="li"
              key={title}
              sx={{
                display: "flex",
                alignItems: "flex-start",
                gap: 2,
                px: 2.5,
                py: 1.75,
                borderTop: index > 0 ? "1px solid" : "none",
                borderColor: "divider",
              }}
            >
              <MaterialSymbol name={symbol} size={26} sx={{ color: "primary.main", mt: 0.25, flexShrink: 0 }} />
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ fontWeight: 600, lineHeight: 1.3 }}>
                  {index + 1}. {title}
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                  {text}
                </Typography>
              </Box>
            </Box>
          ))}
        </Box>
      </Paper>

      {/* ---- Past visits ---- */}
      <Paper variant="outlined" sx={{ mt: 2, borderRadius: 3, overflow: "hidden" }}>
        <ButtonBase
          onClick={() => router.push(VISITS_PATH)}
          sx={{ width: "100%", display: "flex", alignItems: "center", gap: 2, px: 2.5, py: 2, textAlign: "left" }}
        >
          <MaterialSymbol name="history" size={26} sx={{ color: "primary.main", flexShrink: 0 }} />
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography sx={{ fontWeight: 600, lineHeight: 1.3 }}>Past visits</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
              What you ordered and what you paid
            </Typography>
          </Box>
          <MaterialSymbol name="chevron_right" size={24} sx={{ color: "text.secondary" }} />
        </ButtonBase>
      </Paper>

      {/* ---- Account: log out only between visits ---- */}
      <Box sx={{ mt: 4, mb: 3 }}>
        {seated ? (
          <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 3, textAlign: "left" }}>
            <Typography sx={{ fontWeight: 700 }}>
              {tableNumber ? `You're seated at Table ${tableNumber}` : "You're seated at a table"}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
              You can log out once your visit ends.
            </Typography>
            <Button
              variant="contained"
              fullWidth
              size="large"
              onClick={() => router.push(MENU_PATH)}
              sx={{ mt: 2, minHeight: 52, borderRadius: 999, fontSize: "1rem" }}
            >
              Back to your table
            </Button>
          </Paper>
        ) : (
          <Button
            variant="outlined"
            fullWidth
            size="large"
            onClick={logoutCustomer}
            startIcon={<MaterialSymbol name="logout" size={22} />}
            sx={{ minHeight: 52, borderRadius: 999, fontSize: "1rem" }}
          >
            Log out
          </Button>
        )}
      </Box>
    </CustomerPageContainer>
  );
};

export default CustomerHome;
