"use client";

import { ThemeProvider, CssBaseline } from "@mui/material";
import { AppRouterCacheProvider } from "@mui/material-nextjs/v16-appRouter";
import { createTheme } from "@mui/material/styles";
import { MenuProvider } from "@/contexts/MenuContext";
import { NotificationProvider } from "@/contexts/NotificationContext";
import { OrderProvider } from "@/contexts/OrderContext";
import { TableOrderProvider } from "@/contexts/TableOrderContext";
import NotificationSystem from "@/components/common/NotificationSystem";
import VisitEndedWatcher from "@/components/customer/VisitEndedWatcher";
import Layout from "@/components/Layout.jsx";

const theme = createTheme({
  palette: {
    primary: {
      main: "#C01E2E",
    },
    secondary: {
      main: "#14171a",
    },
    warning: {
      main: "#b45309",
      dark: "#92400e",
      contrastText: "#ffffff",
    },
    info: {
      main: "#01579b",
      contrastText: "#ffffff",
    },
    error: {
      main: "#d32f2f",
      dark: "#b71c1c",
    },
    text: {
      primary: "#000000",
      secondary: "#5b6670",
    },
    background: {
      default: "#ffffff",
      paper: "#ffffff",
    },
  },
  typography: {
    fontFamily: "var(--font-open-sans), sans-serif",
    h1: {
      fontFamily: "var(--font-montserrat), sans-serif",
      fontWeight: 600,
    },
    h2: {
      fontFamily: "var(--font-montserrat), sans-serif",
      fontWeight: 600,
    },
    h3: {
      fontFamily: "var(--font-montserrat), sans-serif",
      fontWeight: 600,
    },
    h4: {
      fontFamily: "var(--font-montserrat), sans-serif",
      fontWeight: 600,
    },
    h5: {
      fontFamily: "var(--font-montserrat), sans-serif",
      fontWeight: 600,
    },
    h6: {
      fontFamily: "var(--font-montserrat), sans-serif",
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
    MuiChip: {
      styleOverrides: {
        // Outlined warning text sits on tinted panels so need to use use the darker shade
        root: ({ ownerState, theme }) =>
          ownerState.variant === "outlined" && ownerState.color === "warning"
            ? {
                color: theme.palette.warning.dark,
                borderColor: theme.palette.warning.dark,
              }
            : {},
      },
    },
  },
});

// Client-side providers for every page. Kept out of app/layout.jsx so the layout stays a server component.
export default function Providers({ children }) {
  return (
    // Sends MUI styles in <head> during SSR, so no <style> tags end up in <body> to break hydration
    <AppRouterCacheProvider>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <NotificationProvider>
          <MenuProvider>
            <OrderProvider>
              <TableOrderProvider>
                <Layout>{children}</Layout>
                <VisitEndedWatcher />
              </TableOrderProvider>
            </OrderProvider>
          </MenuProvider>
          <NotificationSystem />
        </NotificationProvider>
      </ThemeProvider>
    </AppRouterCacheProvider>
  );
}
