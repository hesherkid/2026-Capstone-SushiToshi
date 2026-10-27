import { useState } from "react";
import { Box, Alert, Button, Tabs, Tab } from "@mui/material";
import { RefreshCw, Clock, ChefHat, CircleCheck } from "lucide-react";
import ManagementHeader from "@/components/common/ManagementHeader";
import ManagementGrid from "@/components/common/ManagementGrid";
import StatCard from "@/components/home/StatCard";
import { statsGrid } from "@/components/home/homeLayout";
import { useOrderDashboard } from "../context/OrderDashboardContext";
import { useOrderDashboardActions } from "../hooks/useOrderDashboardActions";
import { ORDER_STATUS } from "../hooks/useOrderData";
import { getOrderColumns, STATUS_OPTIONS } from "./OrderColumns";
import OrderDetailDialog from "./dialogs/OrderDetailDialog";

const TABS = [
  ORDER_STATUS.PENDING,
  ORDER_STATUS.PROCESSING,
  ORDER_STATUS.DELIVERED,
];

// Open orders oldest first so the longest-waiting table is at the top; finished orders newest first
const sortFor = (status) => [
  {
    field: "createdAt",
    sort: status === ORDER_STATUS.DELIVERED ? "desc" : "asc",
  },
];

const OrderDashboardInner = () => {
  const {
    isLoading,
    error,
    setError,
    orders,
    orderCounts,
    fetchOrders,
    openOrder,
  } = useOrderDashboard();
  const actions = useOrderDashboardActions();
  const [activeTab, setActiveTab] = useState(ORDER_STATUS.PENDING);

  const rows = orders.filter((order) => order.status === activeTab);

  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", p: 3 }}>
      <ManagementHeader
        title="Orders"
        actions={
          <Button
            onClick={() => fetchOrders()}
            disabled={isLoading}
            sx={{ gap: 1 }}
          >
            <RefreshCw />
            Refresh
          </Button>
        }
      />

      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {actions.actionError && (
        <Alert
          severity="error"
          onClose={actions.clearActionError}
          sx={{ mb: 2 }}
        >
          {actions.actionError}
        </Alert>
      )}

      <Box sx={{ ...statsGrid, mb: 3 }}>
        <StatCard
          label="Orders Pending"
          value={orderCounts[ORDER_STATUS.PENDING]}
          icon={Clock}
          loading={isLoading}
          highlight={orderCounts[ORDER_STATUS.PENDING] > 0}
        />
        <StatCard
          label="Orders Processing"
          value={orderCounts[ORDER_STATUS.PROCESSING]}
          icon={ChefHat}
          loading={isLoading}
        />
        <StatCard
          label="Delivered - Seated Tables"
          value={orderCounts[ORDER_STATUS.DELIVERED]}
          icon={CircleCheck}
          loading={isLoading}
        />
      </Box>

      <Tabs
        value={activeTab}
        onChange={(e, val) => setActiveTab(val)}
        sx={{ mb: 2 }}
      >
        {TABS.map((status) => (
          <Tab
            key={status}
            value={status}
            label={`${STATUS_OPTIONS[status].label} (${orderCounts[status]})`}
          />
        ))}
      </Tabs>

      <ManagementGrid
        key={activeTab}
        rows={rows}
        columns={getOrderColumns({
          view: (order) => openOrder(order.orderId),
          approve: actions.approve,
          markAllDelivered: actions.markAllDelivered,
        })}
        loading={isLoading}
        getRowId={(row) => row.orderId}
        initialState={{
          sorting: { sortModel: sortFor(activeTab) },
          pagination: { paginationModel: { pageSize: 25 } },
        }}
        localeText={{
          noRowsLabel: `No ${STATUS_OPTIONS[activeTab].label.toLowerCase()} orders`,
        }}
      />

      <OrderDetailDialog />
    </Box>
  );
};

export default OrderDashboardInner;
