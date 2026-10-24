"use client";

import React from "react";
import { Box, Typography, Alert } from "@mui/material";
import {
  Clock,
  ClipboardList,
  Bell,
  Users,
  QrCode,
  MapPin,
  Menu as MenuIcon,
  Group,
  Contact,
  Tag,
  UserCog,
  BarChart,
  TrendingUp,
  Timer,
  RefreshCw,
  Eye,
  DollarSign,
  CalendarDays,
} from "lucide-react";
import TableIcon from "@/components/common/TableIcon";
import StatCard from "@/components/home/StatCard";
import ShortcutCard from "@/components/home/ShortcutCard";
import ServiceRequestAlerts from "@/components/home/ServiceRequestAlerts";
import useRestaurantOverview from "@/components/home/useRestaurantOverview";
import useStaffFirstName from "@/components/home/useStaffFirstName";
import {
  gridColumns,
  statsGrid,
  todayLabel,
} from "@/components/home/homeLayout";

const shortcutSections = (pendingRequests) => [
  {
    title: "Service",
    items: [
      {
        title: "Dining Sessions",
        description: "Live dining sessions, table requests and bills",
        href: "/dashboard/sessions",
        icon: Clock,
        badge: pendingRequests,
      },
      {
        title: "Tables",
        description: "Table status and availability",
        href: "/dashboard/tables",
        icon: TableIcon,
      },
      {
        title: "Orders",
        description: "Incoming orders for the kitchen",
        href: "/staff/dashboard/orders",
        icon: ClipboardList,
      },
    ],
  },
  {
    title: "Restaurant setup",
    items: [
      {
        title: "QR Codes",
        description: "Print table QR codes and Wi-Fi cards",
        href: "/admin/qr-codes",
        icon: QrCode,
      },
      {
        title: "Locations & Menus",
        description: "Locations, menu types and assignments",
        href: "/location/menu-location",
        icon: MapPin,
      },
      {
        title: "Pricing",
        description: "Buffet prices, GST, gratuity and leftover fee",
        href: "/admin/pricing",
        icon: DollarSign,
      },
      {
        title: "Holidays",
        description: "Load and approve holidays for holiday pricing",
        href: "/admin/holidays",
        icon: CalendarDays,
      },
      {
        title: "Menu Items",
        description: "Dishes, images, tags and menus",
        href: "/admin/menu-items",
        icon: MenuIcon,
      },
      {
        title: "Menu Categories",
        description: "Categories and order limits",
        href: "/admin/menu-categories",
        icon: Group,
      },
      {
        title: "Tags",
        description: "Dietary and allergen tags",
        href: "/tags/tag-management",
        icon: Tag,
      },
      {
        title: "Staff",
        description: "Accounts, roles and passwords",
        href: "/admin/staff",
        icon: UserCog,
      },
      {
        title: "Customers",
        description: "Registered customer accounts",
        href: "/admin/customers",
        icon: Contact,
      },
    ],
  },
  {
    title: "Analytics",
    items: [
      {
        title: "Analytics Overview",
        description: "How are we doing today",
        href: "/analytics/analytic-page",
        icon: BarChart,
      },
      {
        title: "Item Performance",
        description: "Best and worst selling items",
        href: "/analytics/item-performance",
        icon: TrendingUp,
      },
      {
        title: "Order Timing",
        description: "Busy hours and wait times",
        href: "/analytics/order-timing",
        icon: Timer,
      },
      {
        title: "Table Turnover",
        description: "How quickly tables turn over",
        href: "/analytics/table-turnover",
        icon: RefreshCw,
      },
      {
        title: "Browsing Behavior",
        description: "What guests look at on the menu",
        href: "/analytics/browsing-behavior",
        icon: Eye,
      },
    ],
  },
];

// Home page for admins
export const AdminHome = ({ userEmail }) => {
  const firstName = useStaffFirstName(userEmail);
  const { sessions, requests, newRequestIds, activeTables, loading, error } =
    useRestaurantOverview();

  return (
    <Box
      sx={{
        width: "100%",
        maxWidth: 1200,
        mx: "auto",
        textAlign: "left",
        display: "flex",
        flexDirection: "column",
        gap: 3,
        pt: 7,
      }}
    >
      <Box>
        <Typography variant="h4" component="h1">
          Welcome back{firstName ? `, ${firstName}` : ""}
        </Typography>
        <Typography color="text.secondary">
          {todayLabel()} · Here&apos;s how the restaurant is doing right now.
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      <Box sx={statsGrid}>
        <StatCard
          label="Active sessions"
          value={sessions.length}
          icon={Users}
          loading={loading}
          href="/dashboard/sessions"
        />
        <StatCard
          label="Tables in use"
          value={activeTables}
          icon={TableIcon}
          loading={loading}
          href="/dashboard/tables"
        />
        <StatCard
          label="Pending requests"
          value={requests.length}
          icon={Bell}
          loading={loading}
          highlight={requests.length > 0}
          href="/dashboard/sessions"
        />
      </Box>

      {!loading && requests.length > 0 && (
        <ServiceRequestAlerts
          requests={requests}
          newRequestIds={newRequestIds}
        />
      )}

      {shortcutSections(requests.length).map((section) => (
        <Box component="section" key={section.title} aria-label={section.title}>
          <Typography variant="h6" component="h2" sx={{ mb: 1.5 }}>
            {section.title}
          </Typography>
          <Box sx={gridColumns(260)}>
            {section.items.map((item) => (
              <ShortcutCard key={item.href} {...item} />
            ))}
          </Box>
        </Box>
      ))}
    </Box>
  );
};

export default AdminHome;
