"use client";

import {
  Drawer,
  List,
  ListItemIcon,
  ListItemText,
  ListItemButton,
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
} from "lucide-react";

const menuItemsByRole = {
  customer: [
    { icon: MenuSquare, label: "Menu", path: "/menu/full-menu" },
     { icon: Users, label: "Current Orders", path: "/dashboard/orders" },
    { icon: Clock, label: "Bills Summary", path: "/dashboard/bills" },
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
    { icon: MapPin, label: "Manage Locations", path: "/location/menu-location" },
    { icon: Users, label: "Orders", path: "/staff/dashboard/orders" },
    { icon: MenuIcon, label: "Manage Menu Items", path: "/admin/menu-items" },
    { icon: Group, label: "Manage Menu Categories", path: "/admin/menu-categories" },
    { icon: Users, label: "Manage Staff", path: "/admin/staff" },
    { icon: Tag, label: "Manage Tags", path: "/tags/tag-management" },
    { icon: QrCode, label: "QR Codes", path: "/admin/qr-codes" },
    { icon: BarChart, label: "Analytics", path: "/analytics/analytic-page" },
  ],
};

const AppNavigationDrawer = ({ open, role, onClose, onNavigate }) => (
  <Drawer
    anchor="left"
    open={open}
    onClose={onClose}
    sx={{
      "& .MuiDrawer-paper": {
        top: { xs: 56, sm: 64 },
      },
    }}
  >
    <List sx={{ width: 320, pt: 3 }}>
      {(menuItemsByRole[role] || []).map((item) => {
        const Icon = item.icon;

        return (
          <ListItemButton
            key={item.path}
            onClick={() => onNavigate(item.path)}
            sx={{
              cursor: "pointer",
              py: 3,
              "& .MuiListItemIcon-root": {
                minWidth: 56,
                "& svg": {
                  width: 24,
                  height: 24,
                },
              },
              "& .MuiListItemText-primary": {
                fontSize: "1.1rem",
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
  </Drawer>
);

export default AppNavigationDrawer;