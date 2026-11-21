import { useState } from "react";
import { Box, Alert, Button, TextField } from "@mui/material";
import { Search } from "lucide-react";
import ManagementGrid from "@/components/common/ManagementGrid";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";
import { useOrderHistory, toDateInput } from "../hooks/useOrderHistory";
import { useOrderDays } from "../hooks/useOrderDays";
import HistoryDatePicker from "./HistoryDatePicker";
import { getSessionHistoryColumns } from "./SessionHistoryColumns";
import SessionHistoryDialog from "./dialogs/SessionHistoryDialog";

// Look up past orders by date range. The date pickers mark the days that have orders.
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

  const { orderDays, loadMonth, isLoading: isLoadingDays } = useOrderDays();
  const dayMarks = { orderDays, loadMonth, isLoadingDays };

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
        <LocalizationProvider dateAdapter={AdapterDayjs}>
          <HistoryDatePicker
            id="history-from"
            label="From"
            value={filters.from}
            onChange={(value) => setFilter("from", value)}
            maxDate={filters.to || today}
            error={!!fieldErrors.from}
            helperText={fieldErrors.from}
            {...dayMarks}
          />
          <HistoryDatePicker
            id="history-to"
            label="To"
            value={filters.to}
            onChange={(value) => setFilter("to", value)}
            minDate={filters.from || undefined}
            maxDate={today}
            error={!!fieldErrors.to}
            helperText={fieldErrors.to}
            {...dayMarks}
          />
        </LocalizationProvider>
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
