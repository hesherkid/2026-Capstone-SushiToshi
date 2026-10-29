"use client";

import { useState, useEffect, Suspense } from "react";
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
import { loginUser, loginAsGuest, getReusableGuestToken, getAuthToken, isGuestClaims } from "@/utils/auth";
import { decodeToken, extractRoles, isTokenExpired } from "@/config/auth";
import { startTableVisit } from "@/utils/tableVisit";

const googleClientID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;

// Sign-in worked but the table couldn't be joined; its message is shown on the form as-is
class TableJoinError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

const LoginFormContent = () => {
  const router = useRouter();
  const searchParams = useSearchParams();

  // Email/password are uncontrolled and read on submit. Re-rendering the form on
  // every keystroke makes iOS Safari repeatedly offer to save the password.
  const defaultEmail = process.env.NEXT_PUBLIC_DEFAULT_EMAIL || "";
  const defaultPassword = process.env.NEXT_PUBLIC_DEFAULT_PASSWORD || "";

  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [needsVerification, setNeedsVerification] = useState(false);
  // Signed in, but the table couldn't be joined (e.g. staff haven't started it yet)
  const [joinFailed, setJoinFailed] = useState(false);
  // A signed-in customer scanned a table's QR code and is being joined to it
  const [joiningTable, setJoiningTable] = useState(false);

  // Keep the context from the restaurant QR code.
  const locationId = searchParams.get("locationId");
  const tableNumber = searchParams.get("tableNumber");
  // Guests have no account to come back to, so they can only get in by joining a table,
  // and the table comes from its QR code
  const hasTable = !!(locationId && tableNumber);

  // A scanned QR code is a new visit: show its table right away (not the last visit's)
  // and drop the old session so nothing can order onto it
  useEffect(() => {
    startTableVisit({ locationId, tableNumber });
  }, [locationId, tableNumber]);

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

  // Joins the session staff started for the QR code's table. Returns the session ID,
  // or null when the user didn't come from a table QR code.
  const startQrDiningSession = async (accessToken) => {
    if (!locationId || !tableNumber) return null;

    try {
      const { data } = await api.post(
        "/DiningSession/join",
        { Table_Number: Number(tableNumber), Location_Id: Number(locationId) },
        { headers: { Authorization: `Bearer ${accessToken}` } },
      );
      localStorage.setItem("session_id", String(data.session_Id));
      return data.session_Id;
    } catch (err) {
      // e.g. "Table 4 doesn't have an active session yet. Please ask staff to start your table."
      throw new TableJoinError(
        err.response?.data?.message ||
        "We couldn't join your table. Please ask staff for help.",
        err.response?.status,
      );
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
    setJoinFailed(err instanceof TableJoinError);

    if (err instanceof TableJoinError) {
      setNeedsVerification(false);
      setError(err.message);
      return;
    }

    const status = err.response?.status;
    const data = err.response?.data;

    // Rate limited (e.g. lots of guest sign-ins from the restaurant Wi-Fi at once); the response has no body
    if (status === 429) {
      setNeedsVerification(false);
      setError("Lots of people are signing in right now. Please wait a moment and try again.");
      return;
    }

    if (status === 404 || status >= 500 || !err.response) {
      setNeedsVerification(false);
      setError("Something went wrong. Please try again.");
      return;
    }

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

  // --------------------------------------------------
  // EMAIL / PASSWORD LOGIN
  // --------------------------------------------------

  const handleSubmit = async (event) => {
    event.preventDefault();

    // Read before setLoading disables the inputs (disabled fields are left out of FormData)
    const formData = new FormData(event.currentTarget);
    const email = String(formData.get("email") ?? "");
    const password = String(formData.get("password") ?? "");

    setLoading(true);
    setError("");
    setNeedsVerification(false);

    try {
      const response = await loginUser(email, password);

      if (!response.access_token) {
        throw new Error("Login succeeded but no access token was returned.");
      }

      clearGuestSession();
      saveRestaurantContext();
      const sessionId = await startQrDiningSession(response.access_token);

      router.replace(sessionId ? "/menu/full-menu" : "/");
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
    if (!hasTable) return;
    setLoading(true);
    setError("");
    setNeedsVerification(false);

    // A new guest account, for a phone that isn't already a guest
    const newGuestToken = async () => {
      const response = await loginAsGuest();

      if (!response.access_token) {
        throw new Error("Guest login succeeded but no token was returned.");
      }

      localStorage.setItem("guest", "true");
      return response.access_token;
    };

    try {
      saveRestaurantContext();

      // Rescanning the QR code, or another table's, keeps this phone the same guest
      // instead of adding a new "Guest XXXX" participant each time
      const savedToken = getReusableGuestToken();
      let sessionId;

      try {
        sessionId = await startQrDiningSession(savedToken ?? (await newGuestToken()));
      } catch (err) {
        // The saved guest no longer works (e.g. the account was removed): start over as a new guest
        if (!savedToken || err.status !== 401) throw err;
        sessionId = await startQrDiningSession(await newGuestToken());
      }

      router.replace(sessionId ? "/menu/full-menu" : "/");
      router.refresh();
    } catch (err) {
      if (err.response?.status === 401) {
        setJoinFailed(false);
        setError("Something went wrong. Please try again.");
      } else {
        handleAuthError(err);
      }
    } finally {
      setLoading(false);
    }
  };

  // The sign-in already worked and its token is saved, so only the join is tried again
  // (no new guest account, no typing the password again)
  const handleRetryJoin = async () => {
    const token = getAuthToken();
    if (!token) {
      setJoinFailed(false);
      setError("Please sign in again.");
      return;
    }

    setLoading(true);
    setError("");

    try {
      const sessionId = await startQrDiningSession(token);
      router.replace(sessionId ? "/menu/full-menu" : "/");
      router.refresh();
    } catch (err) {
      handleAuthError(err);
    } finally {
      setLoading(false);
    }
  };

  // A customer who signed in first (and saw their home page) then scanned the table's QR code:
  // join with that sign-in instead of asking for the password again. Guests still tap
  // Continue as Guest, and staff accounts never join a table this way
  useEffect(() => {
    if (!locationId || !tableNumber) return;

    const token = getAuthToken();
    if (!token || isTokenExpired(token) || localStorage.getItem("guest") === "true") return;

    const claims = decodeToken(token);
    const isCustomer = extractRoles(claims).some((role) => String(role).toLowerCase() === "customer");
    if (!isCustomer || isGuestClaims(claims)) return;

    setJoiningTable(true);
    handleRetryJoin().finally(() => setJoiningTable(false));
  }, [locationId, tableNumber]);

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
      const sessionId = await startQrDiningSession(data.access_token);

      // Synchronize with the existing auth hook.
      window.dispatchEvent(new Event("authChange"));

      router.replace(sessionId ? "/menu/full-menu" : "/");
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
        disabled={loading || !hasTable}
        aria-describedby={hasTable ? undefined : "guest-needs-table"}
      >
        Continue as Guest
      </Button>

      {!hasTable && (
        <Typography
          id="guest-needs-table"
          variant="body2"
          color="text.secondary"
          sx={{ width: "100%", textAlign: "center", mt: -1 }}
        >
          To continue as a guest, scan the QR code on your table.
        </Typography>
      )}

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

      {joiningTable && (
        <Alert severity="info" icon={<CircularProgress size={20} />} sx={{ width: "100%" }}>
          Joining Table {tableNumber}…
        </Alert>
      )}

      {/* Authentication errors */}
      {error && (
        <Alert
          severity={
            needsVerification ? "warning" : "error"
          }
          sx={{ width: "100%", "& .MuiAlert-message": { flexGrow: 1 } }}
        >
          <Typography variant="body2">
            {error}
          </Typography>

          {joinFailed && (
            <Button
              variant="outlined"
              color="inherit"
              size="small"
              fullWidth
              onClick={handleRetryJoin}
              disabled={loading}
              sx={{ mt: 2, textTransform: "none", fontWeight: 600, }}
            >
              Try again
            </Button>
          )}

          {
            needsVerification && (
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
            )
          }
        </Alert >
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
          id="login-email"
          label="Email Address"
          name="email"
          type="email"
          defaultValue={defaultEmail}
          autoComplete="username"
          inputProps={{ autoCapitalize: "none", autoCorrect: "off", spellCheck: false }}
          required
          fullWidth
          disabled={loading}
        />

        {/* Password */}
        <TextField
          id="login-password"
          label="Password"
          name="password"
          type="password"
          defaultValue={defaultPassword}
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
      </Box>

      {googleClientID && (
        <Divider sx={{ width: "100%" }}>
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
    </AuthCard >
  );
};

const LoginForm = () => (
  <Suspense fallback={null}>
    <LoginFormContent />
  </Suspense>
);

export default LoginForm;
