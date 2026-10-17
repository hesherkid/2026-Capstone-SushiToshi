import React, { useState, useEffect, useCallback } from "react";
import { Box, Alert, Chip } from "@mui/material";
import {
  MailCheck,
  MailWarning,
  Users,
  UserCheck,
  UserPlus,
  UserX,
  Trash2,
} from "lucide-react";
import api from "@/config/api";
import ManagementHeader from "@/components/common/ManagementHeader";
import ManagementGrid from "@/components/common/ManagementGrid";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import { actionsColumn } from "@/components/common/RowActions";
import StatCard from "@/components/home/StatCard";
import { statsGrid } from "@/components/home/homeLayout";
import { formatDate } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import { isActiveAccount, AccountStatusChip } from "@/components/admin/StaffList";
import { useAuth } from "@/hooks/useAuth";

// /customers returns at most 100 per call
const PAGE_LIMIT = 100;
const NEW_CUSTOMER_DAYS = 30;

const daysAgo = (days) => Date.now() - days * 24 * 60 * 60 * 1000;

const customerName = (customer) =>
  `${customer.first_name ?? ""} ${customer.last_name ?? ""}`.trim() || customer.email;

// Actions are only passed for admins
export const getCustomerColumns = ({ onDeactivate, onReactivate, onDelete } = {}) => [
  {
    field: "name",
    headerName: "Name",
    flex: 1,
    minWidth: 150,
    valueGetter: (value, row) =>
      `${row.first_name ?? ""} ${row.last_name ?? ""}`.trim() || "—",
  },
  {
    field: "email",
    headerName: "Email",
    flex: 1.4,
    minWidth: 200,
  },
  {
    field: "is_email_confirmed",
    headerName: "Email Verified",
    type: "boolean",
    flex: 0.8,
    minWidth: 120,
    renderCell: ({ value }) =>
      value ? (
        <Chip icon={<MailCheck size={14} />} label="Verified" color="success" size="small" variant="outlined" />
      ) : (
        <Chip icon={<MailWarning size={14} />} label="Unverified" size="small" variant="outlined" />
      ),
  },
  {
    field: "status",
    headerName: "Status",
    flex: 0.8,
    minWidth: 110,
    renderCell: ({ value }) => <AccountStatusChip status={value} />,
  },
  {
    field: "created_at",
    headerName: "Joined",
    type: "date",
    flex: 0.8,
    minWidth: 120,
    valueGetter: (value) => (value ? new Date(value) : null),
    valueFormatter: (value) => (value ? formatDate(value) : "—"),
  },
  {
    field: "last_Interaction_at",
    headerName: "Last Active",
    type: "date",
    flex: 0.8,
    minWidth: 120,
    valueGetter: (value) => (value ? new Date(value) : null),
    valueFormatter: (value) => (value ? formatDate(value) : "—"),
  },
  actionsColumn(
    (row) => [
      {
        label: "Deactivate customer",
        icon: UserX,
        color: "warning",
        onClick: () => onDeactivate(row),
        hidden: !onDeactivate || !isActiveAccount(row),
      },
      {
        label: "Reactivate customer",
        icon: UserCheck,
        color: "success",
        onClick: () => onReactivate(row),
        hidden: !onReactivate || isActiveAccount(row),
      },
      {
        label: "Delete customer",
        icon: Trash2,
        color: "error",
        onClick: () => onDelete(row),
        hidden: !onDelete,
      },
    ],
    { width: 110 }
  ),
];

// Admin list of registered customer accounts (customers sign themselves up)
const CustomerManagement = () => {
  const [customers, setCustomers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  // { action: "deactivate" | "delete", customer }
  const [pendingAction, setPendingAction] = useState(null);
  const { userRole } = useAuth();
  const isAdmin = userRole === "admin";

  const fetchCustomers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const all = [];
      for (let skip = 0; ; skip += PAGE_LIMIT) {
        const response = await api.get("/customers", {
          params: { skip, limit: PAGE_LIMIT },
        });
        const page = Array.isArray(response.data) ? response.data : [];
        all.push(...page);
        if (page.length < PAGE_LIMIT) break;
      }
      setCustomers(all);
    } catch (err) {
      console.error("Error fetching customers:", err);
      setError("Failed to load customers");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchCustomers();
  }, [fetchCustomers]);

  const runAction = async (request, successMessage, failureMessage) => {
    setError(null);
    setSuccess(null);
    try {
      await request();
      setSuccess(successMessage);
      await fetchCustomers();
    } catch (err) {
      console.error(failureMessage, err);
      const detail = err.response?.data;
      setError(typeof detail === "string" && detail ? detail : failureMessage);
    }
  };

  const setStatus = (customer, status) =>
    api.put(`/customers/${customer.user_id}/status`, { status });

  const reactivate = (customer) =>
    runAction(
      () => setStatus(customer, "Active"),
      `${customerName(customer)} can sign in again`,
      "Failed to reactivate customer"
    );

  const confirmPendingAction = () => {
    const { action, customer } = pendingAction;
    if (action === "deactivate") {
      return runAction(
        () => setStatus(customer, "Inactive"),
        `${customerName(customer)} has been deactivated`,
        "Failed to deactivate customer"
      );
    }
    return runAction(
      () => api.delete(`/customers/${customer.user_id}`),
      `${customerName(customer)} has been deleted`,
      "Failed to delete customer"
    );
  };

  const confirmText = pendingAction && {
    deactivate: {
      title: "Deactivate customer",
      message: `Deactivate ${customerName(pendingAction.customer)}? They won't be able to sign in until you reactivate them.`,
      confirmLabel: "Deactivate",
    },
    delete: {
      title: "Delete customer",
      message: `Delete ${customerName(pendingAction.customer)}? Their name, email and sign-in details are removed. Past orders stay in reports. This can't be undone.`,
      confirmLabel: "Delete",
    },
  }[pendingAction.action];

  const activeCount = customers.filter(
    (c) => String(c.status).toLowerCase() === "active"
  ).length;
  const newCount = customers.filter(
    (c) => c.created_at && new Date(c.created_at).getTime() >= daysAgo(NEW_CUSTOMER_DAYS)
  ).length;

  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", p: 3 }}>
      <ManagementHeader
        title="Customer Management"
      />

      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {success && (
        <Alert severity="success" onClose={() => setSuccess(null)} sx={{ mb: 2 }}>
          {success}
        </Alert>
      )}

      <Box sx={{ ...statsGrid, mb: 3 }}>
        <StatCard label="Total Customers" value={customers.length} icon={Users} loading={loading} />
        <StatCard label="Active Customers" value={activeCount} icon={UserCheck} loading={loading} />
        <StatCard
          label={`New in ${NEW_CUSTOMER_DAYS} Days`}
          value={newCount}
          icon={UserPlus}
          loading={loading}
        />
      </Box>

      <ManagementGrid
        rows={customers}
        columns={getCustomerColumns(
          isAdmin
            ? {
                onDeactivate: (customer) => setPendingAction({ action: "deactivate", customer }),
                onReactivate: reactivate,
                onDelete: (customer) => setPendingAction({ action: "delete", customer }),
              }
            : {}
        )}
        loading={loading}
        getRowId={(row) => row.user_id}
        initialState={{
          sorting: { sortModel: [{ field: "created_at", sort: "desc" }] },
          pagination: { paginationModel: { pageSize: 25 } },
        }}
        localeText={{ noRowsLabel: "No customers yet" }}
      />

      <ConfirmDialog
        open={!!pendingAction}
        title={confirmText?.title}
        message={confirmText?.message}
        confirmLabel={confirmText?.confirmLabel}
        onConfirm={confirmPendingAction}
        onClose={() => setPendingAction(null)}
      />
    </Box>
  );
};

export default CustomerManagement;
