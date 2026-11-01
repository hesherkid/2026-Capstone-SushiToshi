"use client";
import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { Box, CircularProgress } from "@mui/material";
import { useAuth } from "@/hooks/useAuth";

// Route guard: sends signed-out visitors to log in and other roles to /unauthorized.
// The API enforces the same roles; this only keeps people off pages they can't use.
export default function RequireRole({ roles, children }) {
  const router = useRouter();
  const { isAuthenticated, userRole, loading } = useAuth();
  const isAuthorized = !loading && isAuthenticated && roles.includes(userRole);

  useEffect(() => {
    if (loading) return;
    if (!isAuthenticated) {
      router.push("/auth/login");
    } else if (!roles.includes(userRole)) {
      router.push("/unauthorized");
    }
  }, [router, isAuthenticated, userRole, loading, roles]);

  if (!isAuthorized) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
        <CircularProgress />
      </Box>
    );
  }

  return children;
}
