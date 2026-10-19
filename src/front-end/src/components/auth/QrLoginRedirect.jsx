"use client";

import { useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Box, CircularProgress } from "@mui/material";

// Old table QR codes linked here. Joining a table now happens after sign-in on /auth/login,
// so forward the table and location there. Param names differ between the old links.
const QrLoginRedirect = ({ tableParam, locationParam }) => {
  const router = useRouter();
  const searchParams = useSearchParams();

  useEffect(() => {
    const params = new URLSearchParams();
    const tableNumber = searchParams.get(tableParam);
    const locationId = searchParams.get(locationParam);

    if (tableNumber) params.set("tableNumber", tableNumber);
    if (locationId) params.set("locationId", locationId);

    const query = params.toString();
    router.replace(query ? `/auth/login?${query}` : "/auth/login");
  }, [router, searchParams, tableParam, locationParam]);

  return (
    <Box display="flex" justifyContent="center" alignItems="center" minHeight="50vh">
      <CircularProgress />
    </Box>
  );
};

export default QrLoginRedirect;
