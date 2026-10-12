"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import dynamic from "next/dynamic";
import { Box, CircularProgress } from "@mui/material";
import { useAuth } from "@/hooks/useAuth";
//import CustomerManagement from "@/components/admin/CustomerManagement";

const StaffManagementPage = dynamic(
  () => import("@/components/admin/StaffManagementPage"),
  {
    loading: () => (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="100vh"
      >
        <CircularProgress />
      </Box>
    ),
    ssr: false,
  }
);

const StaffPage = () => {
  const router = useRouter();
  const [isLoading, setIsLoading] = useState(true);
  const [isAuthorized, setIsAuthorized] = useState(false);
  const { isAuthenticated, userRole, loading } = useAuth();

  useEffect(() => {
    if (loading) {
      setIsLoading(true);
      return;
    }
    if (!isAuthenticated) {
      router.push("/auth/login");
      return;
    }
    if (userRole !== "admin") {
      router.push("/unauthorized");
      return;
    }
    setIsAuthorized(true);
    setIsLoading(false);
  }, [router, isAuthenticated, userRole, loading]);

  if (isLoading || !isAuthorized) {
    return (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="100vh"
      >
        <CircularProgress />
      </Box>
    );
  }

  return (
      <Box sx={{ flexGrow: 1, p: 0 }}>
        <StaffManagementPage />
      </Box>
  );
};

export default StaffPage;
