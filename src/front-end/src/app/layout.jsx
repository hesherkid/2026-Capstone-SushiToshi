"use client";

import { Geist, Geist_Mono } from "next/font/google";
import "@/app/globals.css";

import { ThemeProvider, CssBaseline } from "@mui/material";
import { createTheme } from "@mui/material/styles";
import { MenuProvider } from "@/contexts/MenuContext";
import { NotificationProvider } from "@/contexts/NotificationContext";
import { OrderProvider } from "@/contexts/OrderContext";
import NotificationSystem from "@/components/common/NotificationSystem";
import Layout from "@/components/Layout.jsx";
import "@/styles/global.css";

import { useEffect, useState, createContext } from "react";

import { extractRoles, mapToAppRole } from '@/config/auth';

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

const theme = createTheme({
  palette: {
    primary: {
      main: "#C01E2E",
    },
    secondary: {
      main: "#14171a",
    },
    text: {
      primary: "#000000",
      secondary: "#657786",
    },
    background: {
      default: "#ffffff",
      paper: "#ffffff",
    },
  },
  typography: {
    fontFamily: "'Open Sans', sans-serif",
    h1: {
      fontFamily: "'Montserrat', sans-serif",
      fontWeight: 600,
    },
    h2: {
      fontFamily: "'Montserrat', sans-serif",
      fontWeight: 600,
    },
    h3: {
      fontFamily: "'Montserrat', sans-serif",
      fontWeight: 600,
    },
    h4: {
      fontFamily: "'Montserrat', sans-serif",
      fontWeight: 600,
    },
    h5: {
      fontFamily: "'Montserrat', sans-serif",
      fontWeight: 600,
    },
    h6: {
      fontFamily: "'Montserrat', sans-serif",
      fontWeight: 600,
    },
  },
  components: {
    MuiButton: {
      defaultProps: {
        disableElevation: true,
      },
      styleOverrides: {
        root: {
          fontSize: { xs: "1rem", sm: "1.05rem" },
          textTransform: "none",
          borderRadius: "8px",
          fontWeight: 600,
          letterSpacing: "0.01em",
          minHeight: "44px",
          paddingInline: { xs: 2, sm: 2.5 },
          paddingBlock: { xs: 1.6, sm: 1.9 },
        },
        contained: {
          boxShadow: "none",
          "&:hover": {
            boxShadow: "none",
          },
        },
        containedPrimary: {
          backgroundColor: "#C01E2E",
          "&:hover": {
            backgroundColor: "#a91824",
          },
        },
        containedSecondary: {
          backgroundColor: "#14171a",
          "&:hover": {
            backgroundColor: "#000000",
          },
        },
        outlined: {
          borderWidth: 1.5,
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          borderRadius: 8,
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundColor: "#C01E2E",
        },
      },
    },
  },
});

export const AuthContext = createContext();

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body className={`${geistSans.variable} ${geistMono.variable}`}>
        <ThemeProvider theme={theme}>
          <CssBaseline />
          <NotificationProvider>
            <MenuProvider>
              <OrderProvider>
                <Layout>{children}</Layout>
              </OrderProvider>
            </MenuProvider>
            <NotificationSystem />
          </NotificationProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}