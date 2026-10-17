import React from "react";
import { Chip } from "@mui/material";
import { Edit, Key, UserX, UserCheck } from "lucide-react";
import ManagementGrid from "@/components/common/ManagementGrid";
import { actionsColumn } from "@/components/common/RowActions";

export const isActiveAccount = (user) =>
  String(user?.status).toLowerCase() === "active";

const getStatusColor = (status) => {
  switch (String(status).toLowerCase()) {
    case "active":
      return "success";
    case "inactive":
      return "error";
    default:
      return "default";
  }
};

// Column definitions for the staff management page.
// onToggleStatus is only passed for admins; isSelf hides it on the signed-in admin's own row.
export const getStaffColumns = ({ onEdit, onChangePassword, onToggleStatus, isSelf = () => false }) => [
  {
    field: "name",
    headerName: "Name",
    flex: 1,
    valueGetter: (value, row) => `${row.first_name ?? ""} ${row.last_name ?? ""}`.trim(),
  },
  {
    field: "email",
    headerName: "Email",
    flex: 1,
  },
  {
    field: "location",
    headerName: "Location",
    flex: 1,
    renderCell: (params) =>
      params.value ? <Chip label={params.value} size="small" /> : null,
  },
  {
    field: "role",
    headerName: "Role",
    flex: 1,
    renderCell: (params) => (
      <Chip
        label={params.value}
        color={params.value === "admin" ? "error" : "primary"}
        size="small"
      />
    ),
  },
  {
    field: "status",
    headerName: "Status",
    flex: 1,
    renderCell: (params) => (
      <Chip
        label={params.value}
        color={getStatusColor(params.value)}
        size="small"
      />
    ),
  },
  actionsColumn((row) => [
    { label: "Edit User", icon: Edit, onClick: () => onEdit(row) },
    {
      label: "Reset Password",
      icon: Key,
      onClick: () => onChangePassword(row),
    },
    {
      label: "Deactivate Account",
      icon: UserX,
      color: "warning",
      onClick: () => onToggleStatus(row),
      hidden: !onToggleStatus || isSelf(row) || !isActiveAccount(row),
    },
    {
      label: "Reactivate Account",
      icon: UserCheck,
      color: "success",
      onClick: () => onToggleStatus(row),
      hidden: !onToggleStatus || isSelf(row) || isActiveAccount(row),
    },
  ], { width: 180 }),
];

const StaffList = ({ staff = [], isLoading = false, onEdit, onChangePassword }) => {
  return (
    <ManagementGrid
      rows={staff}
      columns={getStaffColumns({ onEdit, onChangePassword })}
      loading={isLoading}
      getRowId={(row) => row.user_id}
    />
  );
};

export default StaffList;
