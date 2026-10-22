import React from "react";
import { Box, Toolbar, Typography } from "@mui/material";
import AppBarWithTitle from "./AppBarWithTitle";
import CustomerBottomNav from "./customer/CustomerBottomNav";
import CustomerSideNav, { SIDE_NAV_ATTR, SIDE_NAV_WIDTH } from "./customer/CustomerSideNav";
import DevResetButton from "./dev/DevResetButton";
import { FLOATING_BUTTON_ATTR, FLOATING_BUTTON_SPACE } from "./menu/ReviewOrderButton";

// While the customer side rail is up (desktop), the page and footer sit to its right
const besideSideNav = {
  [`body:has([${SIDE_NAV_ATTR}]) &`]: { ml: { md: `${SIDE_NAV_WIDTH}px` } },
};

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
          ...besideSideNav,
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
          // A quiet line at the end of the page (above the tab bar on phones)
          backgroundColor: "transparent",
          color: "text.secondary",
          // Room for the floating Review order button, so it doesn't cover the copyright
          [`body:has([${FLOATING_BUTTON_ATTR}]) &`]: {
            pb: `${FLOATING_BUTTON_SPACE}px`,
          },
          ...besideSideNav,
        }}
      >
        <Typography component="p" variant="body2" sx={{ fontSize: "0.75rem" }}>
          &copy; {new Date().getFullYear()} Sushi Toshi. All rights reserved.
        </Typography>
      </Box>

      <CustomerSideNav />
      <CustomerBottomNav />
      <DevResetButton />
    </Box>
  );
};

export default Layout;
