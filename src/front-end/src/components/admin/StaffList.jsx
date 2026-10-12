import React from "react";
import { Chip } from "@mui/material";
import { Edit, Key } from "lucide-react";
import ManagementGrid from "@/components/common/ManagementGrid";
import { actionsColumn } from "@/components/common/RowActions";

const getStatusColor = (status) => {
  switch (status) {
    case "active":
      return "success";
    case "inactive":
      return "error";
    default:
      return "default";
  }
};

// Column definitions shared by the staff and customer management pages
export const getStaffColumns = ({ onEdit, onChangePassword }) => [
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
  ]),
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
