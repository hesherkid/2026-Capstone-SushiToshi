"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Box, Button, Typography } from "@mui/material";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import { useAuth } from "@/hooks/useAuth";
import { logoutCustomer } from "@/utils/auth";
import storage from "@/utils/storage";

/**
 * Keeps the browser's back button on this page: behind it are the visit's menu, orders and
 * bill, which are over. Each back press lands on a copy of this page that's pushed again.
 */
const useStayOnPage = () => {
  useEffect(() => {
    const stay = () => window.history.pushState(window.history.state, "", window.location.href);
    stay();
    window.addEventListener("popstate", stay);
    return () => window.removeEventListener("popstate", stay);
  }, []);
};

// Shown to customers once staff close their table's session (see VisitEndedWatcher)
const ThankYouPage = () => {
  const router = useRouter();
  const { isAuthenticated, loading } = useAuth();
  const [isGuest, setIsGuest] = useState(false);

  useStayOnPage();

  // Kept after the visit ends (see endTableVisit), so the next scan reuses the guest
  useEffect(() => {
    setIsGuest(storage.get("guest") === "true");
  }, []);

  // Customers with an account have a home page to go back to, and can log out now
  const hasAccount = !loading && isAuthenticated && !isGuest;

  return (
    <CustomerPageContainer sx={{ maxWidth: 560 }}>
      <Box sx={{ textAlign: "center", pt: { xs: 8, sm: 10 }, pb: 6 }}>

        <Typography variant="h4" component="h1" sx={{ mt: 2, fontWeight: 700 }}>
          Thank you!
        </Typography>

        <Typography sx={{ mt: 1.5, fontSize: "1.1rem" }}>
          Please accept our appreciation and sincere thanks for letting us serve you.
        </Typography>

        <Typography sx={{ mt: 1.5, fontSize: "1.1rem" }}>
          We hope to see you again soon.
        </Typography>

        <Typography color="text.secondary" sx={{ mt: 4 }}>
          Your table&apos;s visit has ended.
        </Typography>

        {hasAccount ? (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5, mt: 4 }}>
            <Button
              variant="contained"
              size="large"
              onClick={() => router.replace("/")}
              sx={{ minHeight: 52, borderRadius: 999, fontSize: "1rem" }}
            >
              Back to home page
            </Button>
            <Button
              variant="outlined"
              size="large"
              onClick={logoutCustomer}
              startIcon={<MaterialSymbol name="logout" size={22} />}
              sx={{ minHeight: 52, borderRadius: 999, fontSize: "1rem" }}
            >
              Log out
            </Button>
          </Box>
        ) : (
          !loading && (
            <>
              <Typography color="text.secondary" sx={{ mt: 1 }}>
                To order again, scan the QR code on your table.
              </Typography>
              {/* What an account would add */}
              <Typography color="text.secondary" sx={{ mt: 1 }}>
                Make an account to save favourites and view past orders.
              </Typography>
              {/* Guests have no home page; sign-in is where they can sign in or make an account.
                  The guest account is kept, so the next scan reuses it */}
              <Box sx={{ display: "flex", flexDirection: "column", mt: 4 }}>
                <Button
                  variant="contained"
                  size="large"
                  onClick={() => router.replace("/auth/login")}
                  sx={{ minHeight: 52, borderRadius: 999, fontSize: "1rem" }}
                >
                  Back to sign in
                </Button>
              </Box>
            </>
          )
        )}
      </Box>
    </CustomerPageContainer>
  );
};

export default ThankYouPage;
