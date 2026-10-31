"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import {
  Alert,
  Box,
  Button,
  ButtonBase,
  CircularProgress,
  Divider,
  Paper,
  TextField,
  Typography,
} from "@mui/material";
import api from "@/config/api";
import {
  fetchMySessionId,
  isSeatGone,
  recheckSeat,
  NOT_SEATED_MESSAGE,
} from "@/services/tableSession";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { setPageLoading } from "@/utils/pageLoading";
import { formatTime } from "@/components/staff/SessionDashboard/utils/sessionHelpers";

const SUCCESS_MESSAGE_MS = 8000;
const NOTE_MAX_LENGTH = 200;
const BILL_PATH = "/dashboard/bills";
// Matches the server's per-table cooldown; a 429 tells us the real time left
const REQUEST_COOLDOWN_SECONDS = 30;

// Tiles show the short label; staff see "Need water refill, napkins"
const COMMON_REQUESTS = [
  "Water refill",
  "Napkins",
  "Utensils",
  "Soy sauce",
  "Wasabi",
  "Ginger",
];

// What staff read: the picked tiles, then anything the guest typed
const buildNotes = (picked, note) => {
  const parts = [];
  if (picked.length) {
    const [first, ...rest] = picked;
    parts.push(
      `Need ${[first.toLowerCase(), ...rest.map((p) => p.toLowerCase())].join(", ")}`,
    );
  }
  if (note.trim()) parts.push(note.trim());
  return parts.join(". ");
};

const getErrorMessage = (err, fallback) => {
  const data = err?.response?.data;
  if (typeof data === "string" && data.trim()) return data;
  return data?.message || data?.detail || data?.title || fallback;
};

// One quick request; picked ones get the same red ring as items in the bag on the menu
const RequestTile = ({ label, selected, onClick }) => (
  <ButtonBase
    aria-pressed={selected}
    onClick={onClick}
    sx={{
      minHeight: 56,
      px: 1.5,
      borderRadius: 3,
      border: "1px solid",
      borderColor: selected ? "primary.main" : "divider",
      boxShadow: selected ? "0 0 0 1px #C01E2E" : "none",
      bgcolor: selected ? "rgba(192, 30, 46, 0.04)" : "background.paper",
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      gap: 1,
      fontSize: "0.95rem",
      fontWeight: 600,
      color: selected ? "primary.main" : "text.primary",
      transition:
        "border-color 160ms ease, box-shadow 160ms ease, background-color 160ms ease",
      "&:hover": { bgcolor: selected ? "rgba(192, 30, 46, 0.08)" : "grey.50" },
      "&.Mui-focusVisible": {
        outline: "2px solid",
        outlineColor: "primary.main",
        outlineOffset: 2,
      },
    }}
  >
    {selected && <MaterialSymbol name="check_circle" filled size={20} />}
    {label}
  </ButtonBase>
);

/**
 * Call server: tap what's needed (or type it), send, and see it waiting until staff pick it up.
 * Asking for the bill lives on the Bill page.
 */
const ServiceRequestForm = () => {
  const router = useRouter();
  const [picked, setPicked] = useState([]);
  const [note, setNote] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const [sessionId, setSessionId] = useState(null);
  const [sessionChecked, setSessionChecked] = useState(false);
  // This table's requests staff haven't picked up yet
  const [waiting, setWaiting] = useState([]);
  // Seconds until this table can send another request
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(() => setSuccess(false), SUCCESS_MESSAGE_MS);
    return () => clearTimeout(timer);
  }, [success]);

  useEffect(() => {
    if (cooldown <= 0) return;
    const timer = setTimeout(() => setCooldown((s) => s - 1), 1000);
    return () => clearTimeout(timer);
  }, [cooldown]);

  // The app bar's spinner shows while we find the table
  useEffect(() => {
    setPageLoading(!sessionChecked);
  }, [sessionChecked]);
  useEffect(() => () => setPageLoading(false), []);

  //Find the current user's dining session right when the page loads
  useEffect(() => {
    const getActiveSession = async () => {
      try {
        // Ask the server (guests included) rather than trusting a saved session_id
        const mySessionId = await fetchMySessionId();
        if (mySessionId) {
          setSessionId(mySessionId);
        } else {
          setError(NOT_SEATED_MESSAGE);
        }
      } catch (err) {
        console.error("Error fetching session:", err);
        setError(
          getErrorMessage(err, "Couldn't find your table. Please try again."),
        );
      } finally {
        setSessionChecked(true);
      }
    };

    getActiveSession();
  }, []);

  // A failed check leaves the list as it was; it's only a progress view
  const fetchWaiting = async (sid = sessionId) => {
    if (!sid) return;
    try {
      const response = await api.get(`/ServiceRequest/by-session/${sid}`);
      setWaiting(Array.isArray(response?.data) ? response.data : []);
    } catch (err) {
      if (isSeatGone(err)) {
        const stillSeatedAt = await recheckSeat();
        setWaiting([]);
        setSessionId(stillSeatedAt);
        if (!stillSeatedAt) setError(NOT_SEATED_MESSAGE);
        return;
      }
      console.error("Error loading service requests:", err);
    }
  };

  useEffect(() => {
    if (sessionId) fetchWaiting(sessionId);
  }, [sessionId]);

  // Requests drop off the list as staff pick them up
  useAutoRefresh(() => fetchWaiting(), { enabled: !!sessionId });

  const togglePick = (request) =>
    setPicked((current) =>
      current.includes(request)
        ? current.filter((r) => r !== request)
        : [...current, request],
    );

  const notes = buildNotes(picked, note);

  //Send the service request to the staff
  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!notes || !sessionId || cooldown > 0) return;

    setIsSubmitting(true);
    setError("");
    setSuccess(false);

    try {
      // The server works out the table and who is asking from the session and login
      await api.post(`/ServiceRequest/${sessionId}`, { notes });
      setSuccess(true);
      setPicked([]);
      setNote("");
      setCooldown(REQUEST_COOLDOWN_SECONDS);
      fetchWaiting();
    } catch (err) {
      // Too soon: keep what they picked and count down to when they can send it
      if (err?.response?.status === 429) {
        setCooldown(
          Number(err.response.data?.retry_after_seconds) ||
            REQUEST_COOLDOWN_SECONDS,
        );
        setError(
          getErrorMessage(
            err,
            "Please wait a moment before sending another request.",
          ),
        );
        return;
      }
      console.error("Service request error:", err);
      setError(
        getErrorMessage(err, "Couldn't send your request. Please try again."),
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  const heading = (
    <Typography
      variant="h5"
      component="h1"
      sx={{ fontWeight: 700, mt: { xs: 3, sm: 2 }, mb: 3, textAlign: "center" }}
    >
      Call your server
    </Typography>
  );

  // Not seated (or the table couldn't be found): just the message, nothing to send to
  if (sessionChecked && !sessionId) {
    return (
      <>
        {heading}
        <Alert severity={error === NOT_SEATED_MESSAGE ? "info" : "error"}>
          {error}
        </Alert>
      </>
    );
  }

  return (
    <Box component="form" onSubmit={handleSubmit} sx={{ textAlign: "left" }}>
      {heading}

      {/* ---- Already asked: so nobody sends the same thing twice ---- */}
      {waiting.length > 0 && (
        <Paper
          variant="outlined"
          sx={{ mb: 3, borderRadius: 3, overflow: "hidden" }}
        >
          <Box
            sx={{
              display: "flex",
              alignItems: "center",
              gap: 1,
              px: 2,
              py: 1.5,
            }}
          >
            <MaterialSymbol
              name="room_service"
              size={22}
              sx={{ color: "warning.main" }}
            />
            <Typography component="h2" sx={{ fontWeight: 700 }}>
              Waiting for your server
            </Typography>
          </Box>
          <Box
            component="ul"
            sx={{ listStyle: "none", p: 0, m: 0 }}
            aria-label="Requests waiting for your server"
          >
            {waiting.map((request) => (
              <Box component="li" key={request.request_id}>
                <Divider />
                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    gap: 2,
                    px: 2,
                    py: 1.5,
                  }}
                >
                  <Typography sx={{ overflowWrap: "anywhere" }}>
                    {request.notes}
                  </Typography>
                  <Typography
                    variant="body2"
                    color="text.secondary"
                    sx={{ flexShrink: 0 }}
                  >
                    {formatTime(request.created_at)}
                  </Typography>
                </Box>
              </Box>
            ))}
          </Box>
        </Paper>
      )}

      {/* ---- What's needed ---- */}
      <Paper
        variant="outlined"
        sx={{ p: { xs: 2.5, sm: 3 }, mb: 2, borderRadius: 3 }}
      >
        <Typography
          variant="subtitle1"
          component="h2"
          sx={{ fontWeight: 700, mb: 2 }}
        >
          What do you need?
        </Typography>

        <Box
          role="group"
          aria-label="Quick requests"
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)" },
            gap: 1.25,
          }}
        >
          {COMMON_REQUESTS.map((request) => (
            <RequestTile
              key={request}
              label={request}
              selected={picked.includes(request)}
              onClick={() => togglePick(request)}
            />
          ))}
        </Box>

        <TextField
          label="Anything else? (optional)"
          value={note}
          onChange={(e) => setNote(e.target.value.slice(0, NOTE_MAX_LENGTH))}
          placeholder="e.g. Could we get a high chair?"
          fullWidth
          multiline
          minRows={2}
          helperText={
            note.length > NOTE_MAX_LENGTH - 40
              ? `${note.length}/${NOTE_MAX_LENGTH}`
              : " "
          }
          sx={{ mt: 2.5, "& .MuiOutlinedInput-root": { borderRadius: 2 } }}
        />

        {error && (
          <Alert severity="error" sx={{ mt: 1 }} onClose={() => setError("")}>
            {error}
          </Alert>
        )}

        {success && (
          <Alert
            severity="success"
            icon={<MaterialSymbol name="check_circle" filled size={22} />}
            sx={{ mt: 1 }}
            onClose={() => setSuccess(false)}
          >
            Sent! Your server will be right over.
          </Alert>
        )}

        <Button
          type="submit"
          variant="contained"
          fullWidth
          size="large"
          disabled={isSubmitting || !notes || !sessionId || cooldown > 0}
          startIcon={
            isSubmitting ? (
              <CircularProgress size={20} color="inherit" />
            ) : (
              <MaterialSymbol name="send" size={20} />
            )
          }
          sx={{ mt: 2, minHeight: 52, borderRadius: 999, fontSize: "1rem" }}
        >
          {isSubmitting
            ? "Sending..."
            : cooldown > 0
              ? `Send another in ${cooldown}s`
              : "Send request"}
        </Button>
      </Paper>

      {/* ---- The bill is asked for on its own page ---- */}
      <Paper variant="outlined" sx={{ borderRadius: 3, overflow: "hidden" }}>
        <ButtonBase
          onClick={() => router.push(BILL_PATH)}
          sx={{
            width: "100%",
            display: "flex",
            alignItems: "center",
            gap: 2,
            px: 2.5,
            py: 2,
            textAlign: "left",
          }}
        >
          <MaterialSymbol
            name="receipt_long"
            size={28}
            sx={{ color: "primary.main" }}
          />
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography sx={{ fontWeight: 700 }}>Ready to pay?</Typography>
            <Typography variant="body2" color="text.secondary">
              Ask for your bill on the Bill page.
            </Typography>
          </Box>
          <MaterialSymbol
            name="chevron_right"
            size={24}
            sx={{ color: "text.secondary" }}
          />
        </ButtonBase>
      </Paper>
    </Box>
  );
};

export default ServiceRequestForm;
