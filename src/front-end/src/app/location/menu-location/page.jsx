"use client";
import { Box } from "@mui/material";
import LocationManagement from "@/components/location/LocationManagement";
import MenuManagement from "@/components/location/MenuManagement";
import RequireRole from "@/components/auth/RequireRole";

const ROLES = ["admin"];

const LocationPage = () => (
  <RequireRole roles={ROLES}>
    <Box sx={{ flexGrow: 1, p: 0 }}>
      <LocationManagement />
      <MenuManagement />
    </Box>
  </RequireRole>
);

export default LocationPage;
