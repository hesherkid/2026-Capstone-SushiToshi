import React, { useState, useEffect, useCallback } from "react";
import { Box, Alert, Chip } from "@mui/material";
import {
  MailCheck,
  MailWarning,
  Users,
  UserCheck,
  UserPlus,
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

// /staff/customers returns at most 100 per call
const PAGE_LIMIT = 100;
const NEW_CUSTOMER_DAYS = 30;

const STATUS_COLORS = {
  active: "success",
  inactive: "default",
  suspended: "warning",
  deleted: "error",
};

const daysAgo = (days) => Date.now() - days * 24 * 60 * 60 * 1000;

const customerName = (customer) =>
  `${customer.first_name ?? ""} ${customer.last_name ?? ""}`.trim() || customer.email;

export const getCustomerColumns = ({ onDelete }) => [
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
    renderCell: ({ value }) => (
      <Chip
        label={value}
        color={STATUS_COLORS[String(value).toLowerCase()] ?? "default"}
        size="small"
      />
    ),
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
        label: "Delete customer",
        icon: Trash2,
        color: "error",
        onClick: () => onDelete(row),
      },
    ],
    { width: 90 }
  ),
];

// Admin list of registered customer accounts (customers sign themselves up)
const CustomerManagement = () => {
  const [customers, setCustomers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [customerToDelete, setCustomerToDelete] = useState(null);

  const fetchCustomers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const all = [];
      for (let skip = 0; ; skip += PAGE_LIMIT) {
        const response = await api.get("/staff/customers", {
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

  const deleteCustomer = async (customer) => {
    setError(null);
    setSuccess(null);
    try {
      await api.delete(`/staff/customers/${customer.user_id}`);
      setSuccess(`${customerName(customer)} has been deleted`);
      await fetchCustomers();
    } catch (err) {
      console.error("Failed to delete customer", err);
      const detail = err.response?.data;
      setError(typeof detail === "string" && detail ? detail : "Failed to delete customer");
    }
  };

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
        columns={getCustomerColumns({ onDelete: setCustomerToDelete })}
        loading={loading}
        getRowId={(row) => row.user_id}
        initialState={{
          sorting: { sortModel: [{ field: "created_at", sort: "desc" }] },
          pagination: { paginationModel: { pageSize: 25 } },
        }}
        localeText={{ noRowsLabel: "No customers yet" }}
      />

      <ConfirmDialog
        open={!!customerToDelete}
        title="Delete customer"
        message={
          customerToDelete &&
          `Delete ${customerName(customerToDelete)}? Their name, email and sign-in details are removed. Past orders stay in reports. This can't be undone.`
        }
        confirmLabel="Delete"
        onConfirm={() => deleteCustomer(customerToDelete)}
        onClose={() => setCustomerToDelete(null)}
      />
    </Box>
  );
};

export default CustomerManagement;
