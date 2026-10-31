"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Alert, Box, Button, Divider, Paper, Typography } from "@mui/material";
import useAuth from "@/hooks/useAuth";
import { decodeToken } from "@/config/auth";
import { getAuthToken, isGuestClaims } from "@/utils/auth";
import { fetchPastVisits } from "@/services/myItems";
import { setPageLoading } from "@/utils/pageLoading";
import { parseUtcDate, RESTAURANT_TIME_ZONE } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import MaterialSymbol from "@/components/common/MaterialSymbol";

const LOGIN_PATH = "/auth/login";
const MENU_PATH = "/menu/full-menu";
// Longer orders fold away behind "Show all"
const ITEMS_SHOWN = 5;

const money = (value) => `$${Number(value).toFixed(2)}`;

// e.g. "Fri, Oct 24, 2026", in the restaurant's time zone
const visitDate = (value) =>
  parseUtcDate(value)?.toLocaleDateString("en-CA", {
    weekday: "short",
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: RESTAURANT_TIME_ZONE,
  }) ?? "";

const tableText = (numbers) =>
  numbers.length === 0 ? "" : `${numbers.length > 1 ? "Tables" : "Table"} ${numbers.join(", ")}`;

const VisitCard = ({ visit }) => {
  const [showAll, setShowAll] = useState(false);
  const items = showAll ? visit.items : visit.items.slice(0, ITEMS_SHOWN);
  const hidden = visit.items.length - ITEMS_SHOWN;

  return (
    <Paper component="article" variant="outlined" sx={{ borderRadius: 3, p: 2.5, textAlign: "left" }}>
      <Typography component="h2" sx={{ fontWeight: 700 }}>
        {visitDate(visit.started_at)}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {[visit.location_name, tableText(visit.table_numbers)].filter(Boolean).join(" · ")}
      </Typography>

      <Divider sx={{ my: 1.5 }} />

      <Typography variant="subtitle2" component="h3" sx={{ fontWeight: 700, mb: 0.5 }}>
        What you ordered
      </Typography>
      {visit.items.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          Others at your table did the ordering.
        </Typography>
      ) : (
        <Box component="ul" sx={{ listStyle: "none", p: 0, m: 0 }}>
          {items.map((item) => (
            <Box component="li" key={item.item_id} sx={{ display: "flex", gap: 1, py: 0.25 }}>
              <Typography variant="body2" sx={{ fontWeight: 700, minWidth: 28 }}>
                {item.quantity}×
              </Typography>
              <Typography variant="body2">{item.name}</Typography>
            </Box>
          ))}
        </Box>
      )}
      {hidden > 0 && (
        <Button size="small" onClick={() => setShowAll(!showAll)} sx={{ mt: 0.5, ml: -1 }}>
          {showAll ? "Show less" : `Show all ${visit.items.length}`}
        </Button>
      )}

      {visit.bills.length > 0 && (
        <>
          <Divider sx={{ my: 1.5 }} />
          {visit.bills.map((bill) => (
            <Box key={bill.bill_id} sx={{ display: "flex", justifyContent: "space-between", gap: 2 }}>
              <Typography variant="body2" color="text.secondary">
                {/* A name tells a table's bills apart; one bill is just the total */}
                {visit.bills.length > 1 ? `${bill.bill_name || `Bill #${bill.bill_id}`} total` : "Bill total"}
                {` · ${bill.guest_count} ${bill.guest_count === 1 ? "guest" : "guests"}`}
              </Typography>
              <Typography sx={{ fontWeight: 700 }}>{money(bill.total)}</Typography>
            </Box>
          ))}
        </>
      )}
    </Paper>
  );
};

/**
 * Past visits for customers with an account: the date and location of each ended visit, what
 * they ordered and what the bill came to. Guests go to the menu, signed-out visitors to sign in.
 */
const PastVisitsPage = () => {
  const router = useRouter();
  const { isAuthenticated, loading: authLoading } = useAuth();
  const [visits, setVisits] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (authLoading) return;
    const token = getAuthToken();
    if (token && isGuestClaims(decodeToken(token))) {
      router.replace(MENU_PATH);
      return;
    }
    if (!isAuthenticated) {
      router.replace(LOGIN_PATH);
      return;
    }

    setPageLoading(true);
    fetchPastVisits()
      .then(setVisits)
      .catch((err) => {
        console.error("Error loading past visits:", err);
        setError(err?.response?.data?.message || "Couldn't load your past visits. Please try again.");
      })
      .finally(() => setPageLoading(false));
  }, [authLoading, isAuthenticated, router]);

  useEffect(() => () => setPageLoading(false), []);

  return (
    <CustomerPageContainer sx={{ maxWidth: 560 }}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mt: { xs: 3, sm: 2 }, mb: 3 }}>
        <Button
          onClick={() => router.push("/")}
          aria-label="Back to home"
          sx={{ minWidth: 0, p: 1, borderRadius: 999, color: "text.primary" }}
        >
          <MaterialSymbol name="arrow_back" size={24} />
        </Button>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700 }}>
          Past visits
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {visits?.length === 0 && (
        <Alert severity="info">
          No past visits yet. Your visits show up here once your table&apos;s visit has ended.
        </Alert>
      )}

      {visits?.length > 0 && (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mb: 4 }}>
          {visits.map((visit) => (
            <VisitCard key={visit.session_id} visit={visit} />
          ))}
        </Box>
      )}
    </CustomerPageContainer>
  );
};

export default PastVisitsPage;
