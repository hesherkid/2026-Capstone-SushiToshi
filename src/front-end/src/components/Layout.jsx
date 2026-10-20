import React from "react";
import { Box, Toolbar, Typography } from "@mui/material";
import AppBarWithTitle from "./AppBarWithTitle";
import CustomerBottomNav from "./customer/CustomerBottomNav";

const Layout = ({ children }) => {
  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        minHeight: "100vh",
      }}
    >
      <AppBarWithTitle />
      <Toolbar />

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          display: "flex",
          flexDirection: "column",
          // Phones get thin gutters so customer pages use most of the screen
          px: { xs: 1.5, sm: 2 },
          py: { xs: 2, sm: 4 },
        }}
      >
        {children}
      </Box>

      <Box
        component="footer"
        sx={{
          // Phones show the copyright in the side menu instead
          display: { xs: "none", md: "block" },
          py: 2,
          px: 3,
          mt: "auto",
          backgroundColor: "#14171a",
          color: "#657786",
          textAlign: "center",
        }}
      >
        <Typography component="p" variant="body2">
          &copy; {new Date().getFullYear()} Sushi Toshi. All rights reserved.
        </Typography>
      </Box>

      <CustomerBottomNav />
    </Box>
  );
};

export default Layout;
