import React from "react";
import { Box, Toolbar, Typography } from "@mui/material";
import AppBarWithTitle from "./AppBarWithTitle";
import CustomerBottomNav from "./customer/CustomerBottomNav";
import { FLOATING_BUTTON_ATTR, FLOATING_BUTTON_SPACE } from "./menu/ReviewOrderButton";

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
          py: 2,
          px: 3,
          mt: "auto",
          textAlign: "center",
          // Phones and tablets: a quiet line at the end of the page, above the tab bar.
          // Desktop: the dark bar
          backgroundColor: { xs: "transparent", md: "#14171a" },
          color: { xs: "text.secondary", md: "#657786" },
          // Room for the floating Review order button, so it doesn't cover the copyright
          [`body:has([${FLOATING_BUTTON_ATTR}]) &`]: {
            pb: `${FLOATING_BUTTON_SPACE}px`,
          },
        }}
      >
        <Typography component="p" variant="body2" sx={{ fontSize: { xs: "0.75rem", md: "0.875rem" } }}>
          &copy; {new Date().getFullYear()} Sushi Toshi. All rights reserved.
        </Typography>
      </Box>

      <CustomerBottomNav />
    </Box>
  );
};

export default Layout;
