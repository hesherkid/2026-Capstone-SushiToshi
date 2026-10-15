"use client";

import React from "react";
import { Box, CircularProgress } from "@mui/material";
import useAuth from "@/hooks/useAuth";
import AdminHome from "@/components/home/AdminHome";
import StaffHome from "@/components/home/StaffHome";
import CustomerHome from "@/components/home/CustomerHome";

// "/" shows a different home page per role
const HomePage = () => {
  const { isAuthenticated, userRole, userEmail, loading } = useAuth();

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (isAuthenticated && userRole === "admin") {
    return <AdminHome userEmail={userEmail} />;
  }

  if (isAuthenticated && userRole === "staff") {
    return <StaffHome userEmail={userEmail} />;
  }

  return <CustomerHome />;
};

export default HomePage;
