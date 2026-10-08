"use client";
import React, { useState } from "react";
import NextLink from "next/link";
import { forgotPassword } from "@/utils/auth";
import {
  Box,
  Button,
  Link,
  TextField,
  Alert,
  CircularProgress,
} from "@mui/material";
import { Lock } from "lucide-react";
import AuthCard from "@/components/auth/AuthCard";

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
    <AuthCard
      title="Reset Password"
      icon={<Lock size={40} color="#dc2626" />}
      maxWidth={500}
      gap={3}
    >
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

      <Box
        component="form"
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
        >
          {loading ? (
            <CircularProgress size={24} sx={{ color: "white" }} />
          ) : (
            "Send Reset Link"
          )}
        </Button>

        <Box sx={{ textAlign: "center", marginTop: 2 }}>
          <Link
            component={NextLink}
            href="/auth/login"
            underline="hover"
            sx={{
              display: "inline-block",
              fontSize: "0.875rem",
              padding: "0.5rem 1rem",
              color: "blue.600",
              "&:hover": { color: "blue.800" },
            }}
          >
            Back to Login
          </Link>
        </Box>
      </Box>
    </AuthCard>
  );
};

export default ForgotPasswordForm;