import { Box, Alert, Button } from "@mui/material";
import { alpha } from "@mui/material/styles";
import { RefreshCw, Users, Bell } from "lucide-react";
import TableIcon from "@/components/common/TableIcon";
import ManagementHeader from "@/components/common/ManagementHeader";
import ManagementGrid from "@/components/common/ManagementGrid";
import StatCard from "@/components/home/StatCard";
import { statsGrid } from "@/components/home/homeLayout";
import { useSession } from "../context/SessionContext";
import { useSessionDashboardActions } from "../hooks/useSessionDashboardActions";
import { getSessionColumns } from "./SessionColumns";
import DialogContainer from "./dialogs/DialogContainer";

const gridSx = {
  "& .row-needs-help": {
    bgcolor: (theme) => alpha(theme.palette.warning.main, 0.12),
    "&:hover": {
      bgcolor: (theme) => alpha(theme.palette.warning.main, 0.2),
    },
  },
};

const rowClassName = ({ row }) => (row.requestCount > 0 ? "row-needs-help" : "");

const SessionDashboardInner = () => {
  const {
    isLoading,
    error,
    setError,
    sessions,
    requestsBySession,
    sessionSummary,
    refreshData,
  } = useSession();
  const actions = useSessionDashboardActions();

  // Tables asking for help first, then newest sessions
  const rows = sessions
    .map((session) => ({
      ...session,
      requestCount: requestsBySession[session.session_Id]?.length || 0,
    }))
    .sort(
      (a, b) => b.requestCount - a.requestCount || b.session_Id - a.session_Id
    );

  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", p: 3 }}>
      <ManagementHeader
        title="Dining Sessions"
        addLabel="New Session"
        onAdd={actions.newSession}
        actions={
          <Button onClick={refreshData} disabled={isLoading} sx={{ gap: 1 }}>
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

      <Box sx={{ ...statsGrid, mb: 3 }}>
        <StatCard
          label="Active Sessions"
          value={sessionSummary.activeSessions}
          icon={Users}
          loading={isLoading}
        />
        <StatCard
          label="Tables In Use"
          value={sessionSummary.tablesInUse}
          icon={TableIcon}
          loading={isLoading}
        />
        <StatCard
          label="Pending Requests"
          value={sessionSummary.pendingRequests}
          icon={Bell}
          loading={isLoading}
          highlight={sessionSummary.pendingRequests > 0}
        />
      </Box>

      <ManagementGrid
        rows={rows}
        columns={getSessionColumns(actions)}
        loading={isLoading}
        getRowId={(row) => row.session_Id}
        getRowClassName={rowClassName}
        initialState={{ pagination: { paginationModel: { pageSize: 25 } } }}
        localeText={{ noRowsLabel: "No active sessions" }}
        sx={gridSx}
      />

      <DialogContainer />
    </Box>
  );
};

export default SessionDashboardInner;
