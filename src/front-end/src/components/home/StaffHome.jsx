"use client";

import React from "react";
import { Box, Typography, Alert } from "@mui/material";
import { Clock, Table, ClipboardList, Bell, Users } from "lucide-react";
import StatCard from "@/components/home/StatCard";
import ShortcutCard from "@/components/home/ShortcutCard";
import ServiceRequestAlerts from "@/components/home/ServiceRequestAlerts";
import OrderAlerts from "@/components/home/OrderAlerts";
import useRestaurantOverview from "@/components/home/useRestaurantOverview";
import useStaffFirstName from "@/components/home/useStaffFirstName";
import { gridColumns, statsGrid, todayLabel } from "@/components/home/homeLayout";

// Home page for staff: live floor status, table requests, open orders and quick links
export const StaffHome = ({ userEmail }) => {
  const firstName = useStaffFirstName(userEmail);
  const { sessions, requests, newRequestIds, orders, newOrderIds, activeTables, loading, error } =
    useRestaurantOverview({ includeOrders: true });

  const shortcuts = [
    { title: "Sessions", description: "Seat guests, manage sessions and bills", href: "/dashboard/sessions", icon: Clock, badge: requests.length },
    { title: "Tables", description: "Table status and availability", href: "/dashboard/tables", icon: Table },
    { title: "Orders", description: "Incoming orders for the kitchen", href: "/staff/dashboard/orders", icon: ClipboardList, badge: orders.length },
  ];

  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", textAlign: "left", display: "flex", flexDirection: "column", gap: 3, pt: 7 }}>
      <Box>
        <Typography variant="h4" component="h1">
          Welcome{firstName ? `, ${firstName}` : ""}
        </Typography>
        <Typography color="text.secondary">{todayLabel()}</Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      <Box sx={statsGrid}>
        <StatCard label="Tables active" value={activeTables} icon={Table} loading={loading} href="/dashboard/tables" />
        <StatCard label="Active sessions" value={sessions.length} icon={Users} loading={loading} href="/dashboard/sessions" />
        <StatCard
          label="Pending requests"
          value={requests.length}
          icon={Bell}
          loading={loading}
          highlight={requests.length > 0}
          href="/dashboard/sessions"
        />
      </Box>

      <Box component="section" aria-labelledby="shortcuts-heading">
        <Typography id="shortcuts-heading" variant="h6" component="h2" sx={{ mb: 1.5 }}>
          Shortcuts
        </Typography>
        <Box sx={gridColumns(240)}>
          {shortcuts.map((shortcut) => (
            <ShortcutCard key={shortcut.href} {...shortcut} />
          ))}
        </Box>
      </Box>

      <Box component="section" aria-labelledby="requests-heading">
        <Typography id="requests-heading" variant="h6" component="h2" sx={{ mb: 1.5 }}>
          Table requests
        </Typography>
        {!loading && (
          <ServiceRequestAlerts requests={requests} newRequestIds={newRequestIds} />
        )}
      </Box>

      <Box component="section" aria-labelledby="orders-heading">
        <Typography id="orders-heading" variant="h6" component="h2" sx={{ mb: 1.5 }}>
          Orders
        </Typography>
        {!loading && <OrderAlerts orders={orders} newOrderIds={newOrderIds} />}
      </Box>

    </Box>
  );
};

export default StaffHome;
