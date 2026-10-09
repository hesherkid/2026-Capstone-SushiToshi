"use client";

import React, { useEffect, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import publicApi from "@/config/publicApi";
import {api} from "@/config/api"
import { Container, Box, Typography, CircularProgress } from "@mui/material";

function JoinInner() {
  const searchParams = useSearchParams();
  const tableNumber = searchParams.get("table");
  const locationNumber = searchParams.get("location")

useEffect(() => {
  const joinAsGuest = async () => {
    if (!tableNumber || !locationNumber) {
      console.error('Missing table or location number');
      return;
    }

    try {
      const guestOid = `guest-${crypto.randomUUID()}`;
      
      // ✅ DEFINE payload FIRST
      const payload = {
        TableId: parseInt(tableNumber),      // Use PascalCase for C#
        LocationId: parseInt(locationNumber),
        GuestName: localStorage.getItem("access_token") || "guest"
      };

      // const res = await publicApi.post(
      //   `/DiningSession/addguestparticipant/v2`,
      //   payload
      // );
      const res = await api.post(
        `/DiningSession/addguestparticipant/v2`,
         payload
      );

      console.log('Success response:', res.data.returnedSession);
      

      // Store guest session
      localStorage.setItem("session_id", res.data.returnedSession.sessionId);
      localStorage.setItem("guest_oid", guestOid);
      localStorage.setItem("tableNumber", tableNumber);
      localStorage.setItem("locationId", locationNumber);

      // Redirect to home or menu
      window.location.href = "/";
      
    } catch (err) {
      console.error(err)
      alert(err?.response?.data?.message || "Unable to join this table.");
      window.location.href = "/";
    }
  };

  joinAsGuest();
}, [tableNumber, locationNumber]); // ← Add locationNumber to dependencies

  return (
    <Container>
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="80vh"
        flexDirection="column"
      >
        <Typography variant="h5" align="center" marginBottom={2}>
          Joining Table {tableNumber || "..."}
        </Typography>
        <CircularProgress />
      </Box>
    </Container>
  );
}

export default function Join() {
  return (
    <Suspense fallback={<div>Loading...</div>}>
      <JoinInner />
    </Suspense>
  );
}
