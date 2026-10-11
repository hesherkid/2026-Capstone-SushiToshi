"use client";

import {
  Drawer,
  List,
  ListItemIcon,
  ListItemText,
  ListItemButton,
  Box, 
  Button,
} from "@mui/material";
import {
  Menu as MenuIcon,
  MenuSquare,
  Home,
  Clock,
  Users,
  BarChart,
  Tag,
  QrCode,
  Phone,
  MapPin,
  Table,
  Group,
  LogOut,
} from "lucide-react";

const menuItemsByRole = {
  customer: [
    { icon: MenuSquare, label: "Menu", path: "/menu/full-menu" },
     { icon: Users, label: "Table Orders", path: "/dashboard/orders" },
    { icon: Clock, label: "Bill Summary", path: "/dashboard/bills" },
    { icon: Phone, label: "Call Server", path: "/dashboard/call-server" },
  ],
  staff: [
    { icon: Home, label: "Home", path: "/" },
    { icon: Clock, label: "Sessions", path: "/dashboard/sessions" },
    { icon: Table, label: "Tables", path: "/dashboard/tables" },
    { icon: Users, label: "Orders", path: "/staff/dashboard/orders" },
    { icon: QrCode, label: "QR Codes", path: "/admin/qr-codes" },
  ],
  admin: [
    { icon: Home, label: "Home", path: "/" },
    { icon: Clock, label: "Sessions", path: "/dashboard/sessions" },
    { icon: Table, label: "Tables", path: "/dashboard/tables" },
    { icon: Users, label: "Orders", path: "/staff/dashboard/orders" },
    { icon: MenuIcon, label: "Manage Menu Items", path: "/admin/menu-items" },
    { icon: Tag, label: "Manage Tags", path: "/tags/tag-management" },
    { icon: Group, label: "Manage Menu Categories", path: "/admin/menu-categories" },
    { icon: Users, label: "Manage Staff", path: "/admin/staff" },
    { icon: MapPin, label: "Manage Locations", path: "/location/menu-location" },
    { icon: QrCode, label: "QR Codes", path: "/admin/qr-codes" },
    { icon: BarChart, label: "Analytics", path: "/analytics/analytic-page" },
  ],
};

const AppNavigationDrawer = ({ open, role, onClose, onNavigate, onLogout }) => (
  <Drawer
    anchor="left"
    open={open}
    onClose={onClose}
    sx={{
      "& .MuiDrawer-paper": {
        top: { xs: 70, sm: 64 },
        height: { xs: "calc(100% - 70px)", sm: "calc(100% - 64px)" },
      },
    }}
  >
    <List sx={{ width: 320, pt: 5, pl: 2}}>
      {(menuItemsByRole[role] || []).map((item) => {
        const Icon = item.icon;

        return (
          <ListItemButton
            key={item.path}
            onClick={() => onNavigate(item.path)}
            sx={{
              cursor: "pointer",
              py: { xs: 1, sm: 1.25, md: 1.5 },
              px: { xs: 2, sm: 2.5, md: 3 },
              "& .MuiListItemIcon-root": {
                minWidth: 36,
                "& svg": {
                  width: 22,
                  height: 22,
                },
              },
              "& .MuiListItemText-primary": {
                fontSize: "1rem",
                fontWeight: 500,
              },
            }}
          >
            <ListItemIcon>
              <Icon />
            </ListItemIcon>
            <ListItemText primary={item.label} />
          </ListItemButton>
        );
      })}
    </List>

    <Box
    sx={{
        mt: "auto",
        p: 2,
        borderTop: 1,
        borderColor: "divider",
        flexShrink: 0,
    }}
    >
        <Button
            fullWidth
            variant="outlined"
            startIcon={<LogOut size={20} />}
            onClick={onLogout}
            sx={{
            justifyContent: "center",
            textTransform: "none",
            py: 1.25,
            px: 2,
            borderRadius: 2,
            }}
        >
            Logout
        </Button>
    </Box>

    </Drawer>
    );

export default AppNavigationDrawer;