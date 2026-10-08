"use client";
import React, { useState } from "react";
import Link from "next/link";
import { forgotPassword } from "@/utils/auth";
import {
  Box,
  Button,
  TextField,
  Typography,
  Paper,
  Alert,
  CircularProgress,
} from "@mui/material";
import { Lock } from "lucide-react";

export const ForgotPasswordForm = () => {
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    setLoading(true);

    try {
      const response = await forgotPassword(
        email.trim().toLowerCase()
      );

      setSuccess(
        response?.message ||
          "If an account exists for this email, a password reset link will be sent. Please check your inbox."
      );
    } catch (err) {
      setError(
        err.response?.data?.message ||
          err.response?.data?.detail ||
          "An error occurred. Please try again later."
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: "100vh",
        backgroundColor: "gray.100",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 1,
      }}
    >
      <Paper
        sx={{
          padding: 1,
          width: "100%",
          backgroundColor: "white",
          borderRadius: 2,
        }}
      >
        <Box
          sx={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: 4,
          }}
        >
          {/* Icon with background */}
          <Box
            sx={{ borderRadius: "50%", backgroundColor: "red.100", padding: 1 }}
          >
            <Lock sx={{ width: 40, height: 40, color: "red.600" }} />
          </Box>

          {error && (
            <Alert severity="error" sx={{ width: "100%", marginBottom: 2 }}>
              {error}
            </Alert>
          )}

          {success && (
            <Alert severity="success" sx={{ width: "100%", marginBottom: 2 }}>
              {success}
            </Alert>
          )}

          <Typography
            component="h1"
            sx={{
              fontSize: "1.25rem",
              fontWeight: "bold",
              textAlign: "center",
            }}
          >
            Reset Password
          </Typography>

          <form
            onSubmit={handleSubmit}
            sx={{
              width: "100%",
              display: "flex",
              flexDirection: "column",
              gap: 3,
            }}
          >
            <TextField
              label="Email Address"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              fullWidth
              autoComplete="email"
              autoFocus
              sx={{ marginBottom: 3 }}
            />

            <Button
              type="submit"
              fullWidth
              variant="contained"
              disabled={loading}
              sx={{
                paddingY: 2,
                marginBottom: 3,
                backgroundColor: "red.600",
                "&:hover": {
                  backgroundColor: "red.700",
                },
                fontSize: "1rem",
                height: 56,
                textTransform: "none",
              }}
            >
              {loading ? (
                <CircularProgress size={24} sx={{ color: "white" }} />
              ) : (
                "Send Reset Link"
              )}
            </Button>

            <Box sx={{ textAlign: "center", marginTop: 2 }}>
              <Link href="/auth/login" passHref>
                <Button
                  sx={{
                    textTransform: "none",
                    fontSize: "0.875rem",
                    padding: "0.5rem 1rem",
                    color: "blue.600",
                    "&:hover": { color: "blue.800" },
                  }}
                >
                  Back to Login
                </Button>
              </Link>
            </Box>
          </form>
        </Box>
      </Paper>
    </Box>
  );
};

export default ForgotPasswordForm;