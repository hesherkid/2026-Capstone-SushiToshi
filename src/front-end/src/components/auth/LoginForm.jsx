"use client";

import { useState, Suspense } from "react";
import {
  Box,
  Button,
  TextField,
  Typography,
  Paper,
  Alert,
  CircularProgress,
  Divider,
} from "@mui/material";

import { Lock } from "lucide-react";
import { useRouter, useSearchParams } from "next/navigation";
import { GoogleLogin, GoogleOAuthProvider } from "@react-oauth/google";

import api from "@/config/api";
import { loginUser } from "@/utils/auth";

const googleClientID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;

const LoginFormContent = () => {
  const router = useRouter();
  const searchParams = useSearchParams();

  const guestEmail = "guestemail@email.com";
  const guestPassword = "GuestUser!";

  const [formData, setFormData] = useState({
    email: process.env.NEXT_PUBLIC_DEFAULT_EMAIL || "",
    password: process.env.NEXT_PUBLIC_DEFAULT_PASSWORD || "",
  });

  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [needsVerification, setNeedsVerification] = useState(false);

  // Keep the context from the restaurant QR code.
  const locationId = searchParams.get("locationId");
  const tableNumber = searchParams.get("tableNumber");

  // --------------------------------------------------
  // HELPERS
  // --------------------------------------------------

  const saveRestaurantContext = () => {
    if (locationId) {
      localStorage.setItem("locationId", locationId);
    }

    if (tableNumber) {
      localStorage.setItem("tableNumber", tableNumber);
    }
  };

  const clearExistingAuthentication = () => {
    localStorage.removeItem("authToken");
    localStorage.removeItem("access_token");
    localStorage.removeItem("tokenExpiresAt");
    localStorage.removeItem("user");
    localStorage.removeItem("guest");
  };

  const getAuthRoute = (path) => {
    const params = new URLSearchParams();

    if (locationId) {
      params.set("locationId", locationId);
    }

    if (tableNumber) {
      params.set("tableNumber", tableNumber);
    }

    const query = params.toString();

    return query ? `${path}?${query}` : path;
  };

  const handleAuthError = (err) => {
    const status = err.response?.status;
    const data = err.response?.data;

    if (status === 403 && data?.requires_email_verification) {
      setNeedsVerification(true);
      setError(data.message || "Please verify your email before signing in.");
      return;
    }

    setNeedsVerification(false);

    setError(
      data?.detail ||
        data?.message ||
        err.message ||
        "Authentication failed. Please try again.",
    );
  };

  const handleChange = (event) => {
    const { name, value } = event.target;

    setFormData((previous) => ({
      ...previous,
      [name]: value,
    }));
  };

  // --------------------------------------------------
  // EMAIL / PASSWORD LOGIN
  // --------------------------------------------------

  const handleSubmit = async (event) => {
    event.preventDefault();

    setLoading(true);
    setError("");
    setNeedsVerification(false);

    try {
      const response = await loginUser(formData.email, formData.password);

      if (!response.access_token) {
        throw new Error("Login succeeded but no access token was returned.");
      }

      clearGuestSession();
      saveRestaurantContext();

      router.replace("/");
      router.refresh();
    } catch (err) {
      handleAuthError(err);
    } finally {
      setLoading(false);
    }
  };

  // --------------------------------------------------
  // GUEST SESSION
  // --------------------------------------------------

  const clearGuestSession = () => {
    localStorage.removeItem("guest");
  };

  const handleContinueAsGuest = async () => {
    setLoading(true);
    setError("");
    setNeedsVerification(false);

    try {
      const response = await loginUser(guestEmail, guestPassword);

      if (!response.access_token) {
        throw new Error("Guest login succeeded but no token was returned.");
      }

      saveRestaurantContext();

      localStorage.setItem("guest", "true");

      router.replace("/");
      router.refresh();
    } catch (err) {
      handleAuthError(err);
    } finally {
      setLoading(false);
    }
  };

  // --------------------------------------------------
  // GOOGLE LOGIN
  // --------------------------------------------------

  const handleGoogleSuccess = async (credentialResponse) => {
    setLoading(true);
    setError("");
    setNeedsVerification(false);

    try {
      if (!credentialResponse.credential) {
        throw new Error("Google credential is missing.");
      }

      const response = await api.post("/auth/google", {
        idToken: credentialResponse.credential,
      });

      const data = response.data;

      if (!data.access_token) {
        throw new Error(
          "Google authentication did not return an access token.",
        );
      }

      clearExistingAuthentication();

      localStorage.setItem("authToken", data.access_token);
      localStorage.setItem("access_token", data.access_token);

      // The JWT itself contains its expiration.
      // No tokenExpiresAt is required when expires_in
      // is not returned.

      if (data.user) {
        localStorage.setItem("user", JSON.stringify(data.user));
      }

      saveRestaurantContext();

      // Synchronize with the existing auth hook.
      window.dispatchEvent(new Event("authChange"));

      router.replace("/");
      router.refresh();
    } catch (err) {
      handleAuthError(err);
    } finally {
      setLoading(false);
    }
  };

  // --------------------------------------------------
  // NAVIGATION
  // --------------------------------------------------

  const handleForgotPassword = () => {
    router.push(getAuthRoute("/auth/forgot-password"));
  };

  const handleRegister = () => {
    router.push(getAuthRoute("/auth/register"));
  };

  const handleResendVerification = () => {
    const url = getAuthRoute("/auth/resend-verification");

    router.push(url);
  };

  // --------------------------------------------------
  // UI
  // --------------------------------------------------

  return (
    <Box
      sx={{
        minHeight: "100vh",
        backgroundColor: "#f3f4f6",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        p: { xs: 2, sm: 4, md: 6 },
      }}
    >
      <Paper
        elevation={3}
        sx={{
          p: { xs: 3, sm: 5, md: 6 },
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
            gap: 3,
          }}
        >
          <Box
            sx={{
              borderRadius: "50%",
              backgroundColor: "#fee2e2",
              p: 2,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            <Lock size={40} color="#dc2626" />
          </Box>

          <Typography
            variant="h4"
            component="h1"
            sx={{
              fontWeight: "bold",
              textAlign: "center",
              color: "#111827",
              fontSize: {
                xs: "1.5rem",
                sm: "2rem",
              },
            }}
          >
            Login to Sushi Toshi
          </Typography>

          {searchParams.get("reset") === "success" && (
            <Alert severity="success" sx={{ width: "100%" }}>
              Your password has been reset successfully. Please sign in with
              your new password.
            </Alert>
          )}

          {/* Authentication errors */}
          {error && (
            <Alert
              severity={needsVerification ? "warning" : "error"}
              sx={{ width: "100%" }}
            >
              <Typography variant="body2">{error}</Typography>

              {needsVerification && (
                <Button
                  variant="text"
                  onClick={handleResendVerification}
                  sx={{
                    mt: 1,
                    color: "#92400e",
                    textTransform: "none",
                    fontWeight: 600,
                  }}
                >
                  Resend Verification Email
                </Button>
              )}
            </Alert>
          )}

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
            {/* Google authentication */}
            {googleClientID && (
              <GoogleOAuthProvider clientId={googleClientID}>
                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "center",
                  }}
                >
                  <GoogleLogin
                    onSuccess={handleGoogleSuccess}
                    onError={() => {
                      setError("Google login failed.");
                    }}
                  />
                </Box>
              </GoogleOAuthProvider>
            )}

            {googleClientID && (
              <Divider>
                <Typography variant="body2" color="text.secondary">
                  OR
                </Typography>
              </Divider>
            )}

            {/* Email */}
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

            {/* Password */}
            <TextField
              label="Password"
              name="password"
              type="password"
              value={formData.password}
              onChange={handleChange}
              autoComplete="current-password"
              required
              fullWidth
              disabled={loading}
            />

            {/* Sign in */}
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
                textTransform: "none",
                fontSize: "1rem",
              }}
            >
              {loading ? (
                <CircularProgress size={24} sx={{ color: "white" }} />
              ) : (
                "Sign In"
              )}
            </Button>

            {/* Guest login */}
            <Button
              type="button"
              onClick={handleContinueAsGuest}
              fullWidth
              variant="outlined"
              disabled={loading}
              sx={{
                borderColor: "#dc2626",
                color: "#dc2626",
                "&:hover": {
                  borderColor: "#b91c1c",
                  backgroundColor: "#fef2f2",
                },
                py: 1.5,
                textTransform: "none",
                fontSize: "1rem",
              }}
            >
              Sign In As Guest
            </Button>

            <Divider />

            {/* Forgot password */}
            <Button
              type="button"
              onClick={handleForgotPassword}
              fullWidth
              variant="text"
              sx={{
                color: "#4b5563",
                textTransform: "none",
              }}
            >
              Forgot Password?
            </Button>

            {/* Registration */}
            <Button
              type="button"
              onClick={handleRegister}
              fullWidth
              variant="text"
              sx={{
                color: "#4b5563",
                textTransform: "none",
              }}
            >
              Create New Account
            </Button>
          </Box>
        </Box>
      </Paper>
    </Box>
  );
};

const LoginForm = () => (
  <Suspense fallback={null}>
    <LoginFormContent />
  </Suspense>
);

export default LoginForm;
