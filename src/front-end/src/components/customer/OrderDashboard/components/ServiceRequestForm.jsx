"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import SendIcon from "@mui/icons-material/Send";
import {
  Box,
  Button,
  Typography,
  TextField,
  Grid,
  Alert,
  CircularProgress,
} from "@mui/material";
import api from "@/config/api";
import { fetchMySessionId, NOT_SEATED_MESSAGE } from "@/services/tableSession";
import CustomerBillDialog from "@/components/customer/CustomerBillDialog";

const SUCCESS_MESSAGE_MS = 8000;
const READY_FOR_BILL = "Ready for bill";

const getErrorMessage = (err, fallback) => {
  const data = err?.response?.data;
  if (typeof data === "string" && data.trim()) return data;
  return data?.message || data?.detail || data?.title || fallback;
};

const ServiceRequestForm = () => {
  const [message, setMessage] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const [sessionId, setSessionId] = useState(null);
  const [billId, setBillId] = useState(null);
  const router = useRouter();

  const commonRequests = [
    "Need water refill",
    "Need napkins",
    "Need utensils",
    "Need soy sauce",
    "Need wasabi",
    "Need ginger",
    READY_FOR_BILL,
  ];

  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(() => setSuccess(false), SUCCESS_MESSAGE_MS);
    return () => clearTimeout(timer);
  }, [success]);

  // Same as the Bill Summary page's button: show the total, then lock the bill and tell staff.
  // Uses the bill this phone last ordered on, or the table's only open bill
  const openMyBill = async () => {
    setError("");
    if (!sessionId) return;
    try {
      const response = await api.get(`/Bill/get_bills/${sessionId}`);
      const openBills = (Array.isArray(response.data) ? response.data : [])
        .filter((bill) => String(bill.status ?? "").toUpperCase() === "OPEN");
      const lastUsed = Number(localStorage.getItem("currentBillId"));
      const mine =
        openBills.find((bill) => Number(bill.bill_id) === lastUsed) ??
        (openBills.length === 1 ? openBills[0] : null);

      if (mine) {
        setBillId(Number(mine.bill_id));
      } else if (openBills.length > 1) {
        // Several parties at the table: let them pick theirs
        router.push("/dashboard/bills");
      } else {
        setError("Your table doesn't have an open bill yet. Your server will open one for you.");
      }
    } catch (err) {
      console.error("Error finding your bill:", err);
      setError(getErrorMessage(err, "Couldn't find your bill. Please try again."));
    }
  };

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
        setError(getErrorMessage(err, "Couldn't find your table. Please try again."));
      }
    };

    getActiveSession();
  }, []);

  //Send the service request to the staff
  const handleSubmit = async (e) => {
    const apiClient = api;
    e.preventDefault();
    if (!message.trim() || !sessionId) return;

    setIsSubmitting(true);
    setError("");
    setSuccess(false);

    // The server works out the table and who is asking from the session and login
    const payload = { notes: message.trim() };

    try {
      const response = await apiClient.post(
        `/ServiceRequest/${sessionId}`,
        payload
      );

      if (response.status < 200 || response.status >= 300) {
        const data = response.data;
        let errorMessage = "Failed to submit request";
        if (data && typeof data === "object" && data.detail) {
          errorMessage = data.detail;
        }
        throw new Error(errorMessage);
      }

      setSuccess(true);
      setMessage("");
    } catch (err) {
      console.error("Service request error:", err);
      setError(getErrorMessage(err, "Failed to submit request"));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Box maxWidth="sm" mx="auto" sx={{ pt: 3 }}>
      <Typography variant="h4" align="center" gutterBottom>
        Request Assistance
      </Typography>

      <Grid container spacing={2} justifyContent="center" mb={3}>
        {commonRequests.map((request) => (
          <Grid key={request}>
            <Button
              variant="outlined"
              onClick={() => (request === READY_FOR_BILL ? openMyBill() : setMessage(request))}
            >
              {request}
            </Button>
          </Grid>
        ))}
      </Grid>

      <Box mb={2}>
        <Typography variant="h6">Custom Message</Typography>
        <TextField
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          placeholder="Type your request here..."
          fullWidth
          multiline
          rows={4}
          variant="outlined"
        />
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {success && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess(false)}>
          Request submitted successfully!
        </Alert>
      )}

      <Box display="flex" justifyContent="center">
        <Button
          onClick={handleSubmit}
          disabled={isSubmitting || !message.trim() || !sessionId}
          variant="contained"
          color="primary"
          startIcon={
            isSubmitting ? <CircularProgress size={20} /> : <SendIcon />
          }
        >
          {isSubmitting ? "Submitting..." : "Send Request"}
        </Button>
      </Box>

      <CustomerBillDialog
        open={!!billId}
        sessionId={sessionId}
        billId={billId}
        onClose={() => setBillId(null)}
      />
    </Box>
  );
};

export default ServiceRequestForm;
