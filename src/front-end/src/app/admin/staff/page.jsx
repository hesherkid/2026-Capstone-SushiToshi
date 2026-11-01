"use client";
import dynamic from "next/dynamic";
import { Box, CircularProgress } from "@mui/material";

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

const StaffPage = () => (
  <Box sx={{ flexGrow: 1, p: 0 }}>
    <StaffManagementPage />
  </Box>
);

export default StaffPage;
