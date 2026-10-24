"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Box, CircularProgress } from "@mui/material";
import HolidayManagement from "@/components/admin/HolidayManagement";
import { useAuth } from "@/hooks/useAuth";

// Admin only, matching the API (HolidayController)
const HolidaysPage = () => {
  const router = useRouter();
  const [isAuthorized, setIsAuthorized] = useState(false);
  const { isAuthenticated, userRole, loading } = useAuth();

  useEffect(() => {
    if (loading) return;
    if (!isAuthenticated) {
      router.push("/auth/login");
      return;
    }
    if (userRole !== "admin") {
      router.push("/unauthorized");
      return;
    }
    setIsAuthorized(true);
  }, [router, isAuthenticated, userRole, loading]);

  if (!isAuthorized) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
        <CircularProgress />
      </Box>
    );
  }

  return <HolidayManagement />;
};

export default HolidaysPage;
