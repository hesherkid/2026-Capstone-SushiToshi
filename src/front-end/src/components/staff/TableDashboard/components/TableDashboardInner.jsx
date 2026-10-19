import { useState } from "react";
import {
  Box,
  Alert,
  Button,
  Tabs,
  Tab,
  ToggleButton,
  ToggleButtonGroup,
} from "@mui/material";
import { Plus, CircleCheck, Users, PowerOff } from "lucide-react";
import ManagementHeader from "@/components/common/ManagementHeader";
import ManagementGrid from "@/components/common/ManagementGrid";
import StatCard from "@/components/home/StatCard";
import { statsGrid } from "@/components/home/homeLayout";
import { useAuth } from "@/hooks/useAuth";
import { useTable } from "../context/TableContext";
import { useTableActions } from "../hooks/useTableActions";
import { TABLE_STATUS } from "../hooks/useTableData";
import { getTableColumns, getGroupColumns, STATUS_OPTIONS } from "./TableColumns";
import DialogContainer from "./dialogs/DialogContainer";

const ALL = "all";

const gridSx = {
  "& .row-inactive": { color: "text.disabled" },
};

const rowClassName = ({ row }) =>
  row.status === TABLE_STATUS.INACTIVE ? "row-inactive" : "";

const TableDashboardInner = () => {
  const {
    isLoading,
    error,
    actionError,
    setActionError,
    setError,
    tables,
    tableGroups,
    tableSummary,
    openDialog,
  } = useTable();
  const actions = useTableActions();
  const { userRole } = useAuth();
  const [activeTab, setActiveTab] = useState(0);
  const [statusFilter, setStatusFilter] = useState(ALL);

  const rows = activeTab === 0 ? tables : tableGroups;
  const filteredRows =
    statusFilter === ALL ? rows : rows.filter((r) => r.status === statusFilter);

  const pageError = error || actionError;

  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", p: 3 }}>
      <ManagementHeader
        title="Tables"
        addLabel="New Table"
        onAdd={() => openDialog("newTable")}
        actions={
          <Button onClick={() => openDialog("newTableGroup")}>
            <Plus />
            New Table Group
          </Button>
        }
      />

      {pageError && (
        <Alert
          severity="error"
          onClose={() => {
            setError(null);
            setActionError(null);
          }}
          sx={{ mb: 2 }}
        >
          {pageError}
        </Alert>
      )}

      <Box sx={{ ...statsGrid, mb: 3 }}>
        <StatCard
          label="Tables Available"
          value={tableSummary.available}
          icon={CircleCheck}
          loading={isLoading}
        />
        <StatCard
          label="Tables In session"
          value={tableSummary.inSession}
          icon={Users}
          loading={isLoading}
        />
        <StatCard
          label="Inactive Tables"
          value={tableSummary.inactive}
          icon={PowerOff}
          loading={isLoading}
        />
      </Box>

      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 2,
          mb: 2,
        }}
      >
        <Tabs value={activeTab} onChange={(e, val) => setActiveTab(val)}>
          <Tab label={`Tables (${tables.length})`} />
          <Tab label={`Table groups (${tableGroups.length})`} />
        </Tabs>

        <ToggleButtonGroup
          size="small"
          exclusive
          value={statusFilter}
          onChange={(e, val) => val && setStatusFilter(val)}
          aria-label="Filter by status"
        >
          <ToggleButton value={ALL}>All</ToggleButton>
          {Object.entries(STATUS_OPTIONS).map(([value, { label }]) => (
            <ToggleButton key={value} value={value}>
              {label}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
      </Box>

      {activeTab === 0 ? (
        <ManagementGrid
          key="tables"
          rows={filteredRows}
          columns={getTableColumns(actions, { canDelete: userRole === "admin" })}
          loading={isLoading}
          getRowId={(row) => row.table_Id}
          getRowClassName={rowClassName}
          initialState={{
            sorting: { sortModel: [{ field: "table_number", sort: "asc" }] },
            pagination: { paginationModel: { pageSize: 25 } },
          }}
          localeText={{ noRowsLabel: "No tables found" }}
          sx={gridSx}
        />
      ) : (
        <ManagementGrid
          key="groups"
          rows={filteredRows}
          columns={getGroupColumns(actions)}
          loading={isLoading}
          getRowId={(row) => row.tableGroup_Id}
          getRowClassName={rowClassName}
          initialState={{
            sorting: { sortModel: [{ field: "group_Name", sort: "asc" }] },
            pagination: { paginationModel: { pageSize: 25 } },
          }}
          localeText={{ noRowsLabel: "No table groups found" }}
          sx={gridSx}
        />
      )}

      <DialogContainer />
    </Box>
  );
};

export default TableDashboardInner;
