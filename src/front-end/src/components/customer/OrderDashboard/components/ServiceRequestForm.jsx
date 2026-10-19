"use client";

import { useState, useEffect } from "react";
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

const ServiceRequestForm = () => {
  const [message, setMessage] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const [sessionId, setSessionId] = useState(null);

  const isGuest = () =>
    typeof window !== "undefined" && localStorage.getItem("guest") === "true";

  const getUserName = () => {
    if (typeof window === "undefined") return "User";
    return localStorage.getItem("user_name") || localStorage.getItem("username") || "User";
  };

  const commonRequests = [
    "Need water refill",
    "Need napkins",
    "Need utensils",
    "Need soy sauce",
    "Need wasabi",
    "Need ginger",
    "Ready for bill",
  ];

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

        // Compatible error extraction (Axios or fetch)
        const message =
          err?.response?.data ||
          err?.message ||
          "Unexpected error occurred";

        setError(message);
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

    const tableId = localStorage.getItem("table_id");
    if (!tableId) {
      setError("Table ID not found. Please scan your table QR code again.");
      setIsSubmitting(false);
      return;
    }

    const payload = {
      Session_Id: sessionId,
      Table_Id: tableId,
      Request_By_Name: localStorage.getItem("guest") === "true" ? "Guest" : getUserName(),
      Request_By_Oid: localStorage.getItem("guest_oid") || "",
      Notes: message,
    };

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
      setTimeout(() => setSuccess(false), 3000);
    } catch (err) {
      console.error("Service request error:", err);
      const errorMessage =
        err?.response?.data ||
        err?.message ||
        "Failed to submit request";
      setError(errorMessage);
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
            <Button variant="outlined" onClick={() => setMessage(request)}>
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
        <Alert severity="success" sx={{ mb: 2 }}>
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
    </Box>
  );
};

export default ServiceRequestForm;
