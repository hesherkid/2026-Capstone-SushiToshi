"use client";

import {
  Drawer,
  List,
  ListItemIcon,
  ListItemText,
  ListItemButton,
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
} from "lucide-react";
import TableIcon from "@/components/common/TableIcon";
import { CUSTOMER_NAV_ITEMS } from "@/components/customer/customerNavItems";
import MaterialSymbol from "@/components/common/MaterialSymbol";

const menuItemsByRole = {
  // Same pages and icons as the phone tab bar
  customer: CUSTOMER_NAV_ITEMS.map(({ label, path, symbol }) => ({
    icon: () => <MaterialSymbol name={symbol} size={22} />,
    label,
    path,
  })),
  staff: [
    { icon: Home, label: "Home", path: "/" },
    { icon: Clock, label: "Dining Sessions", path: "/dashboard/sessions" },
    { icon: TableIcon, label: "Tables", path: "/dashboard/tables" },
    { icon: Users, label: "Orders", path: "/staff/dashboard/orders" },
    { icon: QrCode, label: "QR Codes", path: "/admin/qr-codes" },
  ],
  admin: [
    { icon: Home, label: "Home", path: "/" },
    { icon: Clock, label: "Dining Sessions", path: "/dashboard/sessions" },
    { icon: TableIcon, label: "Tables", path: "/dashboard/tables" },
    { icon: Users, label: "Orders", path: "/staff/dashboard/orders" },
    // `section` starts a labelled group; `divider` starts an unlabelled one
    { icon: Tag, label: "Manage Tags", path: "/tags/tag-management", section: "Management" },
    { icon: MenuIcon, label: "Manage Menu Items", path: "/admin/menu-items" },
    { icon: Group, label: "Manage Menu Categories", path: "/admin/menu-categories" },
    { icon: Users, label: "Manage Staff", path: "/admin/staff" },
    { icon: Contact, label: "Manage Customers", path: "/admin/customers" },
    { icon: MapPin, label: "Manage Locations", path: "/location/menu-location" },
    { icon: QrCode, label: "QR Codes", path: "/admin/qr-codes", divider: true },
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

    <List sx={{ width: 320, pt: 5, pl: 2 }}>
    {(menuItemsByRole[role] || []).map((item) => {
        const Icon = item.icon;

        return (
        <Box key={item.path}>
            {item.section && (
            <>
                <Divider sx={{ my: 2, mr: 2 }} />
                <Box
                sx={{
                    px: { xs: 2, sm: 2.5, md: 3 },
                    pb: 1,
                    color: "text.secondary",
                    fontSize: "0.75rem",
                    fontWeight: 700,
                    textTransform: "uppercase",
                    letterSpacing: "0.08em",
                }}
                >
                {item.section}
                </Box>
            </>
            )}

            {item.divider && (
            <Divider sx={{ my: 2, mr: 2 }} />
            )}

            <ListItemButton
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
        </Box>
        );
    })}
    </List>


    {/* Sticky so Logout stays visible when the menu is taller than the screen.
        Customers don't log out (staff end their visit), so they don't get this */}
    {onLogout && (
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
    )}

    </Drawer>
    );

export default AppNavigationDrawer;