
"use client";

import React, { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import {
  Box,
  Button,
  TextField,
  Typography,
  Paper,
  Alert,
  CircularProgress,
} from "@mui/material";
import { UserPlus, MailCheck } from "lucide-react";
import {
  registerUser,
  resendVerificationEmail,
} from "@/utils/auth";

const RegisterForm = () => {
  const router = useRouter();
  const searchParams = useSearchParams();

  const [formData, setFormData] = useState({
    email: "",
    password: "",
    confirmPassword: "",
    firstName: "",
    lastName: "",
  });

  const [registeredEmail, setRegisteredEmail] = useState("");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);

  // Preserve restaurant context across authentication pages.
  const getAuthRoute = (path) => {
    const params = new URLSearchParams();

    const locationId = searchParams.get("locationId");
    const tableNumber = searchParams.get("tableNumber");

    if (locationId) params.set("locationId", locationId);
    if (tableNumber) params.set("tableNumber", tableNumber);

    const query = params.toString();
    return query ? `${path}?${query}` : path;
  };

  const handleChange = (event) => {
    const { name, value } = event.target;

    setFormData((previous) => ({
      ...previous,
      [name]: value,
    }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();

    setError("");
    setMessage("");

    if (formData.password !== formData.confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    if (formData.password.length < 8) {
      setError("Password must be at least 8 characters long.");
      return;
    }

    setLoading(true);

    try {
      const email = formData.email.trim().toLowerCase();

      // Calls POST /api/auth/register through utils/auth.js
      // Payload: userEmail, userPassword, firstName, lastName.
      await registerUser({
        email,
        password: formData.password,
        firstName: formData.firstName.trim(),
        lastName: formData.lastName.trim(),
      });

      setRegisteredEmail(email);
      setIsSuccess(true);
      setMessage(
        "Registration successful! Please check your email " +
          "and click the verification link before signing in."
      );

      // Clear sensitive fields but retain email for resend.
      setFormData({
        email: "",
        password: "",
        confirmPassword: "",
        firstName: "",
        lastName: "",
      });

      // No JWT storage and no automatic login.
    } catch (err) {
      const status = err.response?.status;
      const data = err.response?.data;

      if (status === 409) {
        setError("This email address is already registered.");
      } else if (status === 400 || status === 422) {
        setError(
          typeof data?.message === "string"
            ? data.message
            : typeof data?.detail === "string"
              ? data.detail
              : "Please check your registration information."
        );
      } else if (!err.response) {
        setError(
          "Unable to connect to the server. Please try again."
        );
      } else {
        setError(
          "Registration failed. Please try again."
        );
      }
    } finally {
      setLoading(false);
    }
  };

  const handleResendVerification = async () => {
    if (!registeredEmail) return;

    setResending(true);
    setError("");
    setMessage("");

    try {
      await resendVerificationEmail(registeredEmail);

      setMessage(
        "If your account requires verification, " +
          "a new verification email will be sent."
      );
    } catch (err) {
      setError(
        err.response?.data?.message ||
          "Unable to resend verification email. Please try again."
      );
    } finally {
      setResending(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: "100vh",
        backgroundColor: "#f3f4f6",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        p: { xs: 2, sm: 4 },
      }}
    >
      <Paper
        elevation={3}
        sx={{
          p: { xs: 3, sm: 5 },
          width: "100%",
          maxWidth: 500,
          backgroundColor: "white",
          borderRadius: 2,
        }}
      >
        <Box
          sx={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: 2.5,
          }}
        >
          {/* Icon */}
          <Box
            sx={{
              borderRadius: "50%",
              backgroundColor: isSuccess ? "#dcfce7" : "#fee2e2",
              p: 2,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            {isSuccess ? (
              <MailCheck size={40} color="#16a34a" />
            ) : (
              <UserPlus size={40} color="#dc2626" />
            )}
          </Box>

          <Typography
            variant="h4"
            component="h1"
            sx={{
              fontWeight: "bold",
              textAlign: "center",
              color: "#111827",
              fontSize: { xs: "1.5rem", sm: "2rem" },
            }}
          >
            {isSuccess ? "Verify Your Email" : "Create Account"}
          </Typography>

          {/* Error */}
          {error && (
            <Alert severity="error" sx={{ width: "100%" }}>
              {error}
            </Alert>
          )}

          {/* Success / informational message */}
          {message && (
            <Alert severity="success" sx={{ width: "100%" }}>
              {message}
            </Alert>
          )}

          {isSuccess ? (
            <Box
              sx={{
                width: "100%",
                display: "flex",
                flexDirection: "column",
                gap: 2,
                textAlign: "center",
              }}
            >
              <Typography variant="body1">
                We sent a verification link to:
              </Typography>

              <Typography
                fontWeight="bold"
                sx={{ overflowWrap: "anywhere" }}
              >
                {registeredEmail}
              </Typography>

              <Typography variant="body2" color="text.secondary">
                Open your email and click the verification link.
                Once verified, you can sign in to Sushi Toshi.
              </Typography>

              <Button
                fullWidth
                variant="contained"
                onClick={() =>
                  router.push(getAuthRoute("/auth/login"))
                }
                sx={{
                  backgroundColor: "#dc2626",
                  "&:hover": {
                    backgroundColor: "#b91c1c",
                  },
                  py: 1.5,
                  textTransform: "none",
                }}
              >
                Go to Login
              </Button>

              <Button
                fullWidth
                variant="outlined"
                disabled={resending}
                onClick={handleResendVerification}
                sx={{
                  borderColor: "#dc2626",
                  color: "#dc2626",
                  textTransform: "none",
                  py: 1.5,
                }}
              >
                {resending ? (
                  <CircularProgress size={24} />
                ) : (
                  "Resend Verification Email"
                )}
              </Button>

              <Typography
                variant="caption"
                color="text.secondary"
              >
                Didn't receive the email? Check your spam folder
                or request another verification link.
              </Typography>
            </Box>
          ) : (
            <Box
              component="form"
              onSubmit={handleSubmit}
              sx={{
                width: "100%",
                display: "flex",
                flexDirection: "column",
                gap: 2.5,
              }}
            >
              <TextField
                label="First Name"
                name="firstName"
                value={formData.firstName}
                onChange={handleChange}
                autoComplete="given-name"
                required
                fullWidth
                disabled={loading}
              />

              <TextField
                label="Last Name"
                name="lastName"
                value={formData.lastName}
                onChange={handleChange}
                autoComplete="family-name"
                required
                fullWidth
                disabled={loading}
              />

              <TextField
                label="Email Address"
                name="email"
                type="email"
                value={formData.email}
                onChange={handleChange}
                autoComplete="email"
                required
                fullWidth
                disabled={loading}
              />

              <TextField
                label="Password"
                name="password"
                type="password"
                value={formData.password}
                onChange={handleChange}
                autoComplete="new-password"
                helperText="Minimum 8 characters"
                inputProps={{ minLength: 8 }}
                required
                fullWidth
                disabled={loading}
              />

              <TextField
                label="Confirm Password"
                name="confirmPassword"
                type="password"
                value={formData.confirmPassword}
                onChange={handleChange}
                autoComplete="new-password"
                required
                fullWidth
                disabled={loading}
              />

              <Button
                type="submit"
                fullWidth
                variant="contained"
                disabled={loading}
                sx={{
                  backgroundColor: "#dc2626",
                  "&:hover": {
                    backgroundColor: "#b91c1c",
                  },
                  color: "white",
                  py: 1.5,
                  height: 52,
                  textTransform: "none",
                  fontSize: "1rem",
                }}
              >
                {loading ? (
                  <CircularProgress
                    size={24}
                    sx={{ color: "white" }}
                  />
                ) : (
                  "Create Account"
                )}
              </Button>

              <Box sx={{ textAlign: "center" }}>
                <Button
                  component={Link}
                  href={getAuthRoute("/auth/login")}
                  variant="text"
                  sx={{
                    textTransform: "none",
                    color: "#4b5563",
                  }}
                >
                  Already have an account? Sign in
                </Button>
              </Box>
            </Box>
          )}
        </Box>
      </Paper>
    </Box>
  );
};

export default RegisterForm;
