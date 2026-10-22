"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Box, ButtonBase, Paper, Typography } from "@mui/material";
import { CUSTOMER_NAV_ITEMS } from "@/components/customer/customerNavItems";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import useCustomerNav from "@/components/customer/useCustomerNav";
import { INACTIVE_COLOR } from "@/components/customer/CustomerBottomNav";

export const SIDE_NAV_WIDTH = 96;
export const SIDE_NAV_ATTR = "data-customer-side-nav";
// The app bar's height from the sm breakpoint up, until it's measured
const APP_BAR_HEIGHT = 64;

// The app bar's height depends on its logo, so measure it rather than guess (as the menu does)
const useAppBarHeight = () => {
  const [height, setHeight] = useState(APP_BAR_HEIGHT);

  useEffect(() => {
    const measure = () => {
      const bar = document.querySelector("header.MuiAppBar-root");
      if (bar?.offsetHeight) setHeight(bar.offsetHeight);
    };
    measure();
    window.addEventListener("resize", measure);
    return () => window.removeEventListener("resize", measure);
  }, []);

  return height;
};

// Desktop version of the phone tab bar: the customer pages down the left side, always in view
const CustomerSideNav = () => {
  const router = useRouter();
  const { show, current } = useCustomerNav();
  const appBarHeight = useAppBarHeight();

  if (!show) return null;

  return (
    <Paper
      component="nav"
      aria-label="Customer navigation"
      {...{ [SIDE_NAV_ATTR]: true }}
      elevation={0}
      square
      sx={{
        display: { xs: "none", md: "flex" },
        flexDirection: "column",
        alignItems: "stretch",
        gap: 1,
        position: "fixed",
        top: appBarHeight,
        bottom: 0,
        left: 0,
        width: SIDE_NAV_WIDTH,
        pt: 2,
        zIndex: (theme) => theme.zIndex.appBar - 1,
        backgroundColor: "background.paper",
        borderRight: "1px solid rgba(0, 0, 0, 0.08)",
      }}
    >
      {CUSTOMER_NAV_ITEMS.map(({ label, path, symbol }) => {
        const selected = current === path;
        return (
          <ButtonBase
            key={path}
            aria-current={selected ? "page" : undefined}
            onClick={() => router.push(path)}
            sx={{
              flexDirection: "column",
              gap: 0.5,
              py: 1,
              color: selected ? "primary.main" : INACTIVE_COLOR,
              transition: "color 150ms ease",
              "&:hover": { color: "primary.main" },
              "&:hover .side-nav-icon": { bgcolor: selected ? "rgba(192, 30, 46, 0.14)" : "grey.100" },
              "&.Mui-focusVisible .side-nav-icon": { outline: "2px solid", outlineColor: "primary.main" },
            }}
          >
            {/* The pill behind the icon marks the page you're on */}
            <Box
              className="side-nav-icon"
              sx={{
                width: 56,
                height: 32,
                borderRadius: 999,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                bgcolor: selected ? "rgba(192, 30, 46, 0.1)" : "transparent",
                transition: "background-color 150ms ease",
              }}
            >
              <MaterialSymbol name={symbol} filled={selected} size={26} />
            </Box>
            <Typography
              component="span"
              sx={{ fontSize: "0.75rem", fontWeight: selected ? 700 : 500, letterSpacing: "0.01em" }}
            >
              {label}
            </Typography>
          </ButtonBase>
        );
      })}
    </Paper>
  );
};

export default CustomerSideNav;
