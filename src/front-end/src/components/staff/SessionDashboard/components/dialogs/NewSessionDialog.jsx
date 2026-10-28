import { useState, useEffect } from "react";
import {
  Box,
  Chip,
  FormControlLabel,
  FormLabel,
  MenuItem,
  Radio,
  RadioGroup,
  TextField,
} from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useSession } from "../../context/SessionContext";
import api from "@/config/api";
import storage from "@/utils/storage";

const NewSessionDialog = ({ open, onClose }) => {
  const { createSession } = useSession();
  const [selectedMenu, setSelectedMenu] = useState("");
  const [selectedLocation, setSelectedLocation] = useState(storage.get("branch-location") ?? "");
  const [tableAssignmentType, setTableAssignmentType] = useState("table");
  const [selectedTable, setSelectedTable] = useState("");
  const [selectedTableGroup, setSelectedTableGroup] = useState("");

  const [availableMenus, setAvailableMenus] = useState([]);
  const [availableLocations, setAvailableLocations] = useState([]);
  const [availableTables, setAvailableTables] = useState([]);
  const [availableTableGroups, setAvailableTableGroups] = useState([]);

  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    api
      .get("/Location")
      .then((response) => setAvailableLocations(response.data))
      .catch((err) => console.error("Error fetching locations:", err));
    api
      .get("/Menu")
      .then((response) => setAvailableMenus(response.data))
      .catch((err) => console.error("Error fetching menus:", err));
  }, [open]);

  // Free tables and table groups at the chosen location
  useEffect(() => {
    setSelectedTable("");
    setSelectedTableGroup("");
    if (!selectedLocation) {
      setAvailableTables([]);
      setAvailableTableGroups([]);
      return;
    }
    const params = { params: { locationId: selectedLocation } };
    api
      .get("/TableEntity/empty", params)
      .then((response) => setAvailableTables(response.data))
      .catch((err) => console.error("Error fetching available tables:", err));
    api
      .get("/TableGroup/available", params)
      .then((response) => setAvailableTableGroups(response.data))
      .catch((err) => console.error("Error fetching available table groups:", err));
  }, [selectedLocation]);

  const ungroupedTables = availableTables.filter((table) => table.tableGroup_Id === null);

  const handleClose = () => {
    setSelectedMenu("");
    setSelectedLocation(storage.get("branch-location") ?? "");
    setTableAssignmentType("table");
    setSelectedTable("");
    setSelectedTableGroup("");
    onClose();
  };

  // A failure is shown as a notification by the session actions; the dialog stays open to retry
  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      const created = await createSession(
        selectedMenu,
        selectedLocation,
        selectedTable,
        selectedTableGroup,
        tableAssignmentType
      );
      if (created) handleClose();
    } finally {
      setSubmitting(false);
    }
  };

  const handleTableAssignmentTypeChange = (event) => {
    setTableAssignmentType(event.target.value);
    setSelectedTable("");
    setSelectedTableGroup("");
  };

  return (
    <FormDialog
      open={open}
      onClose={handleClose}
      title="Create New Session"
      onSubmit={handleSubmit}
      submitLabel="Create Session"
      busyLabel="Creating..."
      busy={submitting}
      submitDisabled={
        !selectedMenu ||
        !selectedLocation ||
        (tableAssignmentType === "table" && !selectedTable) ||
        (tableAssignmentType === "tableGroup" && !selectedTableGroup)
      }
    >
      <TextField
        select
        label="Location"
        required
        value={selectedLocation}
        onChange={(e) => setSelectedLocation(e.target.value)}
      >
        {availableLocations.map((location) => (
          <MenuItem key={location.location_Id} value={location.location_Id}>
            {location.name}
          </MenuItem>
        ))}
      </TextField>

      <TextField
        select
        label="Menu"
        required
        value={selectedMenu}
        onChange={(e) => setSelectedMenu(e.target.value)}
      >
        {availableMenus.map((menu) => (
          <MenuItem key={menu.menu_id} value={menu.menu_id}>
            {menu.name}
          </MenuItem>
        ))}
      </TextField>

      {selectedLocation && (
        <Box sx={{ border: 1, borderColor: "divider", borderRadius: 1, p: 2 }}>
          <FormLabel component="legend">Table Assignment</FormLabel>
          <RadioGroup value={tableAssignmentType} onChange={handleTableAssignmentTypeChange}>
            <FormControlLabel value="table" control={<Radio />} label="Assign Individual Table" />
            <FormControlLabel value="tableGroup" control={<Radio />} label="Assign Table Group" />
          </RadioGroup>

          {tableAssignmentType === "table" && (
            <TextField
              select
              fullWidth
              label="Table"
              required
              value={selectedTable}
              onChange={(e) => setSelectedTable(e.target.value)}
              helperText={
                ungroupedTables.length === 0 &&
                "All tables at this location are currently in use or inactive"
              }
              sx={{ mt: 2 }}
            >
              {ungroupedTables.length === 0 ? (
                <MenuItem disabled>No available tables</MenuItem>
              ) : (
                ungroupedTables.map((table) => (
                  <MenuItem key={table.table_Id} value={table.table_Id}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      Table {table.table_number}
                      <Chip label={`${table.seat_count} seats`} size="small" variant="outlined" />
                    </Box>
                  </MenuItem>
                ))
              )}
            </TextField>
          )}

          {tableAssignmentType === "tableGroup" && (
            <TextField
              select
              fullWidth
              label="Table Group"
              required
              value={selectedTableGroup}
              onChange={(e) => setSelectedTableGroup(e.target.value)}
              helperText={
                availableTableGroups.length === 0 &&
                "All table groups at this location are currently in use or inactive"
              }
              sx={{ mt: 2 }}
            >
              {availableTableGroups.length === 0 ? (
                <MenuItem disabled>No available table groups</MenuItem>
              ) : (
                availableTableGroups.map((group) => (
                  <MenuItem key={group.tableGroup_Id} value={group.tableGroup_Id}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      {group.group_Name}
                      <Chip label={`${group.table_Count} tables`} size="small" variant="outlined" />
                      <Chip label={`${group.total_Seats} seats`} size="small" variant="outlined" />
                    </Box>
                  </MenuItem>
                ))
              )}
            </TextField>
          )}
        </Box>
      )}
    </FormDialog>
  );
};

export default NewSessionDialog;
