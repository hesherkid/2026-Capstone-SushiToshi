
"use client";

import React, { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Box,
  Button,
  Typography,
  Paper,
  Alert,
  CircularProgress,
} from "@mui/material";
import { MailCheck } from "lucide-react";
import { verifyEmail } from "@/utils/auth";

const VerifyEmailForm = () => {
  const router = useRouter();
  const searchParams = useSearchParams();

  const token = searchParams.get("token");

  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);

  const handleVerifyEmail = async () => {
    if (!token) {
      setError("Verification token is missing.");
      return;
    }

    setError("");
    setLoading(true);

    try {
      await verifyEmail(token);
      setIsSuccess(true);
    } catch (err) {
      setError(
        err.response?.data?.message ||
          "Verification failed. The link may be invalid or expired."
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
            gap: 3,
          }}
        >
          <MailCheck size={48} color="#1976d2" />

          <Typography
            component="h1"
            sx={{
              fontSize: "1.25rem",
              fontWeight: "bold",
              textAlign: "center",
            }}
          >
            Verify Your Email
          </Typography>

          {error && (
            <Alert
              severity="error"
              sx={{ width: "100%", marginBottom: 2 }}
            >
              {error}
            </Alert>
          )}

          {isSuccess ? (
            <Box sx={{ width: "100%" }}>
              <Alert
                severity="success"
                sx={{ width: "100%", marginBottom: 2 }}
              >
                Your email has been verified successfully!
                You can now log in to your Sushi Toshi account.
              </Alert>

              <Button
                fullWidth
                variant="contained"
                sx={{
                  paddingY: 2,
                  backgroundColor: "primary.main",
                  "&:hover": {
                    backgroundColor: "primary.dark",
                  },
                  fontSize: "1rem",
                  height: 56,
                  textTransform: "none",
                }}
                onClick={() => router.push("/auth/login")}
              >
                Go to Login
              </Button>
            </Box>
          ) : (
            <Box sx={{ width: "100%" }}>
              <Typography
                sx={{
                  textAlign: "center",
                  marginBottom: 2,
                }}
              >
                Click the button below to verify your email address.
              </Typography>

              {!token && (
                <Alert
                  severity="warning"
                  sx={{ width: "100%", marginBottom: 2 }}
                >
                  Verification token is missing.
                </Alert>
              )}

              <Button
                fullWidth
                variant="contained"
                disabled={loading || !token}
                sx={{
                  paddingY: 2,
                  backgroundColor: "red.600",
                  "&:hover": {
                    backgroundColor: "red.700",
                  },
                  fontSize: "1rem",
                  height: 56,
                  textTransform: "none",
                }}
                onClick={handleVerifyEmail}
              >
                {loading ? (
                  <CircularProgress
                    size={24}
                    sx={{ color: "white" }}
                  />
                ) : (
                  "Verify Email"
                )}
              </Button>

              <Button
                fullWidth
                variant="text"
                sx={{
                  marginTop: 2,
                  textTransform: "none",
                }}
                onClick={() =>
                  router.push("/auth/resend-verification")
                }
              >
                Resend Verification Email
              </Button>
            </Box>
          )}
        </Box>
      </Paper>
    </Box>
  );
};

export default VerifyEmailForm;
