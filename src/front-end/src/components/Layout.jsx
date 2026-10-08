import React from "react";
import { Box, Toolbar, Typography } from "@mui/material";
import AppBarWithTitle from "./AppBarWithTitle";

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
          padding: "2rem 1rem",
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
          backgroundColor: "#14171a",
          color: "#657786",
          textAlign: "center",
        }}
      >
        <Typography component="p" variant="body2">
          &copy; {new Date().getFullYear()} Sushi Toshi. All rights reserved.
        </Typography>
      </Box>
    </Box>
  );
};

export default Layout;
