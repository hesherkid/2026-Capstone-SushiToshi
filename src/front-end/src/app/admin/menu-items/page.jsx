"use client";
import { Box, CircularProgress } from "@mui/material";
import dynamic from "next/dynamic";

const MenuItemManagement = dynamic(
  () => import("@/components/admin/MenuItemManagement"),
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

export default function MenuItemsPage() {
  return (
    <Box sx={{ width: "100%", minHeight: "100vh" }}>
      <MenuItemManagement />
    </Box>
  );
}
