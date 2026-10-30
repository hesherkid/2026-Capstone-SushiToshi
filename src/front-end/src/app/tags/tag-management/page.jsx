"use client";
import dynamic from "next/dynamic";
import { Box, CircularProgress } from "@mui/material";
import RequireRole from "@/components/auth/RequireRole";

const TagManagement = dynamic(() => import("@/components/tags/TagManagement"), {
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
});

const ROLES = ["admin"];

const TagManagementPage = () => (
  <RequireRole roles={ROLES}>
    <Box sx={{ width: "100%", minHeight: "100vh" }}>
      <TagManagement />
    </Box>
  </RequireRole>
);

export default TagManagementPage;
