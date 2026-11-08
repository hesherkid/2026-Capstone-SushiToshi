"use client";

import {
  Drawer,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  ListItemButton,
  ListSubheader,
  Box,
  Button,
  Divider,
} from "@mui/material";
import {
  Menu as MenuIcon,
  Home,
  Clock,
  Users,
  BarChart,
  Tag,
  QrCode,
  MapPin,
  Group,
  Contact,
  LogOut,
  DollarSign,
  CalendarDays,
} from "lucide-react";
import TableIcon from "@/components/common/TableIcon";
import NextLink from "next/link";
import { usePathname } from "next/navigation";

// Staff and admin only: customers get around with the bottom tabs and side rail
// (see CustomerBottomNav and CustomerSideNav)
const menuItemsByRole = {
  staff: [
    { icon: Home, label: "Home", path: "/" },
    {
      icon: Clock,
      label: "Dining Sessions",
      path: "/staff/dashboard/sessions",
    },
    { icon: TableIcon, label: "Tables", path: "/staff/dashboard/tables" },
    { icon: Users, label: "Orders", path: "/staff/dashboard/orders" },
  ],
  admin: [
    { icon: Home, label: "Home", path: "/" },
    {
      icon: Clock,
      label: "Dining Sessions",
      path: "/staff/dashboard/sessions",
    },
    { icon: TableIcon, label: "Tables", path: "/staff/dashboard/tables" },
    { icon: Users, label: "Orders", path: "/staff/dashboard/orders" },
    // `section` starts a labelled group; `divider` starts an unlabelled one
    {
      icon: Tag,
      label: "Manage Tags",
      path: "/tags/tag-management",
      section: "Management",
    },
    { icon: MenuIcon, label: "Manage Menu Items", path: "/admin/menu-items" },
    {
      icon: Group,
      label: "Manage Menu Categories",
      path: "/admin/menu-categories",
    },
    { icon: Users, label: "Manage Staff", path: "/admin/staff" },
    { icon: Contact, label: "Manage Customers", path: "/admin/customers" },
    {
      icon: MapPin,
      label: "Locations & Menus",
      path: "/location/menu-location",
    },
    { icon: DollarSign, label: "Pricing", path: "/admin/pricing" },
    { icon: CalendarDays, label: "Holidays", path: "/admin/holidays" },
    { icon: QrCode, label: "QR Codes", path: "/admin/qr-codes", divider: true },
    { icon: BarChart, label: "Analytics", path: "/analytics/analytic-page" },
  ],
};

// "/" matches itself; other paths also match the pages under them
const isCurrentPath = (pathname, path) =>
  path === "/"
    ? pathname === "/"
    : pathname === path || pathname.startsWith(`${path}/`);

const AppNavigationDrawer = ({ id, open, role, onClose, onLogout }) => {
  const pathname = usePathname() ?? "";

  return (
    <Drawer
      anchor="left"
      open={open}
      onClose={onClose}
      slotProps={{ paper: { id } }}
      sx={{
        "& .MuiDrawer-paper": {
          top: { xs: 70, sm: 64 },
          height: { xs: "calc(100% - 70px)", sm: "calc(100% - 64px)" },
        },
      }}
    >
      <Box component="nav" aria-label="Staff navigation">
        <List sx={{ width: 320, pt: 5, pl: 2 }}>
          {(menuItemsByRole[role] || []).flatMap((item) => {
            const Icon = item.icon;
            const current = isCurrentPath(pathname, item.path);

            return [
              item.section && (
                <Box
                  key={`${item.path}-divider`}
                  component="li"
                  aria-hidden="true"
                  sx={{
                    my: 2,
                    mr: 2,
                    borderTop: 1,
                    borderColor: "divider",
                    listStyle: "none",
                  }}
                />
              ),
              item.section && (
                <ListSubheader
                  key={`${item.path}-section`}
                  disableSticky
                  sx={{
                    px: { xs: 2, sm: 2.5, md: 3 },
                    pb: 1,
                    lineHeight: 1.5,
                    color: "text.secondary",
                    fontSize: "0.75rem",
                    fontWeight: 700,
                    textTransform: "uppercase",
                    letterSpacing: "0.08em",
                  }}
                >
                  {item.section}
                </ListSubheader>
              ),
              item.divider && (
                <Box
                  key={`${item.path}-divider`}
                  component="li"
                  aria-hidden="true"
                  sx={{
                    my: 2,
                    mr: 2,
                    borderTop: 1,
                    borderColor: "divider",
                    listStyle: "none",
                  }}
                />
              ),
              <ListItem key={item.path} disablePadding>
                <ListItemButton
                  component={NextLink}
                  href={item.path}
                  selected={current}
                  aria-current={current ? "page" : undefined}
                  // App bar's menu button gets focus back once the drawer closes
                  onClick={onClose}
                  sx={{
                    py: { xs: 1, sm: 1.25, md: 1.5 },
                    px: { xs: 2, sm: 2.5, md: 3 },
                    "& .MuiListItemIcon-root": {
                      minWidth: 36,
                      "& svg": { width: 22, height: 22 },
                    },
                    "& .MuiListItemText-primary": {
                      fontSize: "1rem",
                      fontWeight: current ? 700 : 500,
                    },
                  }}
                >
                  <ListItemIcon>
                    <Icon aria-hidden="true" />
                  </ListItemIcon>
                  <ListItemText primary={item.label} />
                </ListItemButton>
              </ListItem>,
            ].filter(Boolean);
          })}
        </List>
      </Box>

      {/* Sticky so Logout stays visible when the menu is taller than the screen */}
      <Box
        sx={{
          mt: "auto",
          p: 2,
          borderTop: 1,
          borderColor: "divider",
          flexShrink: 0,
          position: "sticky",
          bottom: 0,
          zIndex: 1,
          bgcolor: "background.paper",
        }}
      >
        <Button
          fullWidth
          variant="outlined"
          startIcon={<LogOut size={20} aria-hidden="true" />}
          onClick={onLogout}
          sx={{ justifyContent: "center", py: 1.25, px: 2, borderRadius: 2 }}
        >
          Logout
        </Button>
      </Box>
    </Drawer>
  );
};

export default AppNavigationDrawer;
