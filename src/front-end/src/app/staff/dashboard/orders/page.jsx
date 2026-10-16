"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Box, CircularProgress } from "@mui/material";
import OrderDashboard from "@/components/staff/Order";
import { useAuth } from "@/hooks/useAuth";

// Staff and admins only
const OrdersPage = () => {
  const router = useRouter();
  const [isAuthorized, setIsAuthorized] = useState(false);
  const { isAuthenticated, userRole, loading } = useAuth();

  useEffect(() => {
    if (loading) return;
    if (!isAuthenticated) {
      router.push("/auth/login");
      return;
    }
    if (userRole !== "admin" && userRole !== "staff") {
      router.push("/unauthorized");
      return;
    }
    setIsAuthorized(true);
  }, [router, isAuthenticated, userRole, loading]);

  if (loading || !isAuthorized) {
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

  return <OrderDashboard />;
};

export default OrdersPage;
