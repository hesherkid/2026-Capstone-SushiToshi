import { useState } from "react";
import { Box, Alert, Button, TextField } from "@mui/material";
import { Search } from "lucide-react";
import ManagementGrid from "@/components/common/ManagementGrid";
import { useOrderHistory, toDateInput } from "../hooks/useOrderHistory";
import { getSessionHistoryColumns } from "./SessionHistoryColumns";
import SessionHistoryDialog from "./dialogs/SessionHistoryDialog";

// Look up past orders by date range.
// One row per dining session; the dialog splits that table's orders by bill.
const OrderHistoryPanel = () => {
  const {
    filters,
    setFilter,
    sessions,
    isLoading,
    error,
    setError,
    fieldErrors,
    hasSearched,
    search,
  } = useOrderHistory();
  const [viewing, setViewing] = useState(null);
  const today = toDateInput(new Date());

  const handleSubmit = (event) => {
    event.preventDefault();
    search();
  };

  const dateSlotProps = (extra) => ({
    inputLabel: { shrink: true },
    htmlInput: { max: today, ...extra },
  });

  return (
    <>
      <Box
        component="form"
        onSubmit={handleSubmit}
        noValidate
        aria-label="Search past orders"
        sx={{
          display: "flex",
          flexWrap: "wrap",
          gap: 2,
          mb: 2,
          alignItems: "flex-start",
        }}
      >
        <TextField
          id="history-from"
          label="From"
          type="date"
          size="small"
          value={filters.from}
          onChange={(e) => setFilter("from", e.target.value)}
          error={!!fieldErrors.from}
          helperText={fieldErrors.from}
          slotProps={dateSlotProps({ max: filters.to || today })}
        />
        <TextField
          id="history-to"
          label="To"
          type="date"
          size="small"
          value={filters.to}
          onChange={(e) => setFilter("to", e.target.value)}
          error={!!fieldErrors.to}
          helperText={fieldErrors.to}
          slotProps={dateSlotProps({ min: filters.from || undefined })}
        />
        <TextField
          id="history-table"
          label="Table #"
          type="number"
          size="small"
          value={filters.tableNumber}
          onChange={(e) => setFilter("tableNumber", e.target.value)}
          error={!!fieldErrors.tablenumber}
          helperText={fieldErrors.tablenumber}
          slotProps={{ htmlInput: { min: 1 } }}
          sx={{ width: 110 }}
        />
        <TextField
          id="history-name"
          label="Guest or bill name"
          size="small"
          value={filters.name}
          onChange={(e) => setFilter("name", e.target.value)}
          sx={{ minWidth: 200 }}
        />
        <Button
          type="submit"
          variant="contained"
          disabled={isLoading}
          startIcon={<Search size={16} />}
          sx={{ height: 40 }}
        >
          Search
        </Button>
      </Box>

      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <ManagementGrid
        rows={sessions}
        columns={getSessionHistoryColumns({ view: setViewing })}
        loading={isLoading}
        getRowId={(row) => row.sessionId}
        initialState={{
          sorting: { sortModel: [{ field: "firstOrderAt", sort: "desc" }] },
          pagination: { paginationModel: { pageSize: 25 } },
        }}
        localeText={{
          noRowsLabel: hasSearched
            ? "No orders match this search"
            : "Choose a date range and press Search",
        }}
      />

      <SessionHistoryDialog
        session={viewing}
        onClose={() => setViewing(null)}
      />
    </>
  );
};

export default OrderHistoryPanel;
