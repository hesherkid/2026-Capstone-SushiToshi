"use client";
import React, { useState, useEffect } from "react";
import {
  FormControl,
  Select,
  MenuItem,
  CircularProgress,
  Alert,
  Box,
  Typography,
  Chip,
} from "@mui/material";
import api from "@/config/api";
import { formatGuestBreakdown } from "@/components/staff/SessionDashboard/utils/sessionHelpers";


const BillSelect = (props) => {
  // Accept both prop names; prefer `session_id` if provided
  const { session_id, _session_id, value, onChange, disabled, message } = props;
//  const effectiveSessionId = session_id ?? _session_id ?? null;
const effectiveSessionId = localStorage.getItem("session_id")

  const [bills, setBills] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [lastRefresh, setLastRefresh] = useState(null);

  const isGuest = () => typeof window !== "undefined" && localStorage.getItem("guest") === "true";

  // TODO: Change to polling or WebSocket for rendering bill updates

  useEffect(() => {
    setLastRefresh(new Date().toLocaleTimeString());
  }, []);

  useEffect(() => {
    const fetchBills = async () => {
    //console.log('Effective Session props:', JSON.stringify(props, null, 2))
    console.log(`Effective Session ${effectiveSessionId}`)
      if (!effectiveSessionId) {
        setBills([]);
        setLoading(false);
        return;
      }

      try {

      setLoading(true);
      setError(null);
      const apiClient = api;
      const response = await apiClient.get(`/Bill/get_bills/${effectiveSessionId}`);

        const billsData = Array.isArray(response.data) ? response.data : [];
        //   const openBills = billsData.filter((bill) => bill.status === "OPEN");
        setBills(billsData);
      } catch (err) {
        if (err?.response?.status === 404) {
        setBills([]);
        setError(null); // Don't show error for empty bills
        } else {
          console.error("Error fetching bills:", err);
          setError(err?.response?.data?.detail || "Failed to fetch bills");
        }
      } finally {
        setLoading(false);
      }
    };

    fetchBills();

    // Poll every 90s
    const intervalId = setInterval(() => {
      fetchBills();
      setLastRefresh(new Date().toLocaleTimeString()); // store a string, not a Date
    }, 90000);

    return () => clearInterval(intervalId);
  }, [effectiveSessionId]);

  // Normalize types so equality checks work even if value is "5" vs 5
  const isValueValid = bills.some(
    (bill) => Number(bill.bill_id) === Number(value)
  );

  const renderBillMenuItem = (bill) => {
  return(
    <MenuItem key={bill.bill_id} value={Number(bill.bill_id)}>
      <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
        <Typography variant="subtitle1">
          {bill.bill_name}
          <Chip
              size="small"
              label={`${bill.total_count || 0} guest${bill.total_count !== 1 ? 's' : ''}`}
              color="primary"
              variant="outlined"
              sx={{ ml: 1 }}
            />
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {formatGuestBreakdown(bill)}
        </Typography>
      </Box>
    </MenuItem>
  );
};

  const renderError = () => {
    if (!error) return null;
    return (
      <Alert severity="error" sx={{ mt: 1 }} onClose={() => setError(null)}>
        {typeof error === "string" ? error : "Failed to fetch bills"}
      </Alert>
    );
  };

  return (
    <Box>
      <FormControl fullWidth disabled={disabled || loading}>
        <Select
          value={isValueValid ? Number(value) : ""}
          onChange={onChange}
          displayEmpty
        >
          <MenuItem value="">
            <em>{message || "Select Bill To Place Order"}</em>
          </MenuItem>

          {loading ? (
            <MenuItem disabled>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <CircularProgress size={20} />
                <span>Loading bills...</span>
              </Box>
            </MenuItem>
          ) : bills.length === 0 ? (
            <MenuItem disabled>No active bills available</MenuItem>
          ) : (
            bills.map(renderBillMenuItem)
          )}
        </Select>
      </FormControl>

      {renderError()}

      <Typography
        variant="caption"
        color="text.secondary"
        sx={{ display: "block", mt: 0.5, textAlign: "right" }}
      >
        Last updated: {lastRefresh}
      </Typography>
    </Box>
  );
};

export default BillSelect;
