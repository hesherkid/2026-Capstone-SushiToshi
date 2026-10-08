"use client";

import { useState } from "react";
import {
  Box,
  Button,
  TextField,
  Typography,
  Alert,
  CircularProgress,
  Divider,
  Link,
} from "@mui/material";

import { Lock } from "lucide-react";
import AuthCard from "@/components/auth/AuthCard";
import NextLink from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { GoogleLogin, GoogleOAuthProvider } from "@react-oauth/google";

import api from "@/config/api";
import { loginUser } from "@/utils/auth";

const googleClientID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;

const LoginForm = () => {
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

    if (
      status === 403 &&
      data?.requires_email_verification
    ) {
      setNeedsVerification(true);
      setError(
        data.message ||
        "Please verify your email before signing in."
      );
      return;
    }

    setNeedsVerification(false);

    setError(
      data?.detail ||
      data?.message ||
      err.message ||
      "Authentication failed. Please try again."
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
      const response = await loginUser(
        formData.email,
        formData.password
      );

      if (!response.access_token) {
        throw new Error(
          "Login succeeded but no access token was returned."
        );
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
      const response = await loginUser(
        guestEmail,
        guestPassword
      );

      if (!response.access_token) {
        throw new Error(
          "Guest login succeeded but no token was returned."
        );
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
          "Google authentication did not return an access token."
        );
      }

      clearExistingAuthentication();

      localStorage.setItem("authToken", data.access_token);
      localStorage.setItem("access_token", data.access_token);

      // The JWT itself contains its expiration.
      // No tokenExpiresAt is required when expires_in
      // is not returned.

      if (data.user) {
        localStorage.setItem(
          "user",
          JSON.stringify(data.user)
        );
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

  const handleResendVerification = () => {
    const url = getAuthRoute("/auth/resend-verification");

    router.push(url);
  };

  // --------------------------------------------------
  // UI
  // --------------------------------------------------

  return (
    <AuthCard
      title="Start your order"
      footer={
        <Typography
          variant="body2"
          sx={{ color: "text.secondary", textAlign: "center" }}
        >
          Don&apos;t have an account?{" "}
          <Link
            component={NextLink}
            href={getAuthRoute("/auth/register")}
            underline="hover"
            sx={{ fontWeight: 700 }}
          >
            Sign Up
          </Link>
        </Typography>
      }
    >
      <Button
        type="button"
        onClick={handleContinueAsGuest}
        fullWidth
        variant="contained"
        disabled={loading}
      >
        Continue as Guest
      </Button>

      <Divider sx={{ width: "100%" }}>
        <Typography variant="body2" color="text.secondary">
          or
        </Typography>
      </Divider>

      {searchParams.get("reset") === "success" && (
        <Alert severity="success" sx={{ width: "100%" }}>
          Your password has been reset successfully.
          Please sign in with your new password.
        </Alert>
      )}

      {/* Authentication errors */}
      {error && (
        <Alert
          severity={
            needsVerification ? "warning" : "error"
          }
          sx={{ width: "100%" }}
        >
          <Typography variant="body2">
            {error}
          </Typography>

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
        <Box sx={{ width: "100%" }}>
          <Typography
            variant="h6"
            component="h2"
            sx={{
              fontWeight: 600,
              color: "#111827",
              mb: 0.25,
            }}
          >
            Have an account?
          </Typography>

          <Typography
            variant="body1"
            sx={{
              color: "#4b5563",
              mb: 1,
            }}
          >
            Sign in to Sushi Toshi
          </Typography>
        </Box>

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

        {/* Forgot password */}
        <Link
          component={NextLink}
          href={getAuthRoute("/auth/forgot-password")}
          sx={{
            display: "block",
            width: "100%",
            textAlign: "right",
            mt: -1,
            color: "#4b5563",
            fontSize: { xs: "0.875rem", sm: "0.95rem" },
            textDecoration: "none",
            "&:hover": { textDecoration: "underline" },
          }}
        >
          Forgot Password?
        </Link>

        {/* Sign in */}
        <Button
          type="submit"
          fullWidth
          variant="contained"
          disabled={loading}
        >
          {loading ? (
            <CircularProgress
              size={24}
              sx={{ color: "white" }}
            />
          ) : (
            "Sign In"
          )}
        </Button>

        {googleClientID && (
          <Divider>
            <Typography variant="body2" color="text.secondary">
              or
            </Typography>
          </Divider>
        )}

        {googleClientID && (
          <GoogleOAuthProvider clientId={googleClientID}>
            <Box sx={{ display: "flex", justifyContent: "center" }}>
              <GoogleLogin
                onSuccess={handleGoogleSuccess}
                onError={() => {
                  setError("Google login failed.");
                }}
              />
            </Box>
          </GoogleOAuthProvider>
        )}
      </Box>
    </AuthCard>
  );
};

export default LoginForm;
