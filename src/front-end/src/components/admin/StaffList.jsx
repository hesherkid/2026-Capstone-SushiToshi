import React from "react";
import { Box, Chip } from "@mui/material";
import { Edit, Key, UserX, UserCheck } from "lucide-react";
import ManagementGrid from "@/components/common/ManagementGrid";
import { actionsColumn } from "@/components/common/RowActions";

export const isActiveAccount = (user) =>
  String(user?.status).toLowerCase() === "active";

const STATUS_COLORS = {
  active: "success",
  inactive: "default",
  suspended: "warning",
  deleted: "error",
};

// Status pill shared by the staff and customer management pages
export const AccountStatusChip = ({ status }) => (
  <Chip
    label={status}
    color={STATUS_COLORS[String(status).toLowerCase()] ?? "default"}
    size="small"
  />
);

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
    field: "locations",
    headerName: "Locations",
    flex: 1,
    // Staff: their assigned locations, with the one they're working at filled in
    renderCell: ({ row }) => {
      if (row.role === "Admin") return <Chip label="All locations" size="small" variant="outlined" />;
      if (!row.locations?.length) return <Chip label="Not assigned" size="small" color="warning" variant="outlined" />;
      // One per line; the row grows to fit (see getRowHeight below)
      return (
        <Box sx={{ display: "flex", flexDirection: "column", alignItems: "flex-start", gap: 0.5, py: 1 }}>
          {row.locations.map((name, i) => (
            <Chip
              key={row.location_ids[i]}
              label={name}
              size="small"
              variant={row.location_ids[i] === row.location_id ? "filled" : "outlined"}
            />
          ))}
        </Box>
      );
    },
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
    renderCell: ({ value }) => <AccountStatusChip status={value} />,
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
      // Taller rows for staff with several locations
      getRowHeight={() => "auto"}
      sx={{ "& .MuiDataGrid-cell": { minHeight: 52 } }}
    />
  );
};

export default StaffList;
