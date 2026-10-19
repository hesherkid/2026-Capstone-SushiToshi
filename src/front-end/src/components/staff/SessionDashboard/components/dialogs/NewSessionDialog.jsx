import { useState, useEffect } from "react";
import {
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  IconButton,
  Box,
  RadioGroup,
  FormControlLabel,
  Radio,
  FormLabel,
  Typography,
  Chip,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { X } from "lucide-react";
import { useSession } from "../../context/SessionContext";
import api from "@/config/api";
import storage from "@/utils/storage";

const NewSessionDialog = ({ open, onClose }) => {
  const { createSession } = useSession();
  const [selectedMenu, setSelectedMenu] = useState("");
  const [selectedLocation, setSelectedLocation] = useState(
    storage.get("branch-location")
  );
  const [tableAssignmentType, setTableAssignmentType] = useState("table");
  const [selectedTable, setSelectedTable] = useState("");
  const [selectedTableGroup, setSelectedTableGroup] = useState("");

  const [availableMenus, setAvailableMenus] = useState([]);
  const [availableLocations, setAvailableLocations] = useState([]);
  const [availableTables, setAvailableTables] = useState([]);
  const [availableTableGroups, setAvailableTableGroups] = useState([]);

  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (open) {
      fetchLocations();
      fetchMenus();
    }
  }, [open]);

  // Fetch available tables and table groups when location changes
  useEffect(() => {
    if (selectedLocation) {
      fetchAvailableTables();
      fetchAvailableTableGroups();
    } else {
      setAvailableTables([]);
      setAvailableTableGroups([]);
      setSelectedTable("");
      setSelectedTableGroup("");
    }
  }, [selectedLocation]);

  const fetchLocations = async () => {
    try {
      const response = await api.get("/Location");
      if (response.status !== 200) throw new Error("Failed to fetch locations");
      setAvailableLocations(response.data);
    } catch (err) {
      console.error("Error fetching locations:", err);
    }
  };

  const fetchMenus = async () => {
    try {
      const response = await api.get("/Menu");
      if (response.status !== 200) throw new Error("Failed to fetch menus");
      setAvailableMenus(response.data);
    } catch (err) {
      console.error("Error fetching menus:", err);
    }
  };

  const fetchAvailableTables = async () => {
    try {
      const response = await api.get("/TableEntity/empty", {
        params: { locationId: selectedLocation },
      });
      if (response.status === 200) {
        setAvailableTables(response.data);
      }
    } catch (err) {
      console.error("Error fetching available tables:", err);
    }
  };

  const fetchAvailableTableGroups = async () => {
    try {
      const response = await api.get("/TableGroup/available", {
        params: { locationId: selectedLocation },
      });
      if (response.status === 200) {
        setAvailableTableGroups(response.data);
      }
    } catch (err) {
      console.error("Error fetching available table groups:", err);
    }
  };

  const handleSubmit = async () => {
    setLoading(true);
    try {
      // Reset form and close
      handleClose();

      // Refresh session list if using context
      if (createSession) {
        createSession(
          selectedMenu,
          selectedLocation,
          selectedTable,
          selectedTableGroup,
          tableAssignmentType
        );
      }
      console.log(createSession)
    } catch (error) {
      console.error("Error creating session:", error);
      const errorMessage =
        error.response.data || "Failed to create session. Please try again.";
      alert(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    setSelectedMenu("");
    setSelectedLocation(storage.get("branch-location"));
    setTableAssignmentType("table");
    setSelectedTable("");
    setSelectedTableGroup("");
    onClose();
  };

  const handleTableAssignmentTypeChange = (event) => {
    console.log("Table Group Change")
    setTableAssignmentType(event.target.value);
    setSelectedTable("");
    setSelectedTableGroup("");
    console.log(tableAssignmentType)
  };

  return (
    <AppDialog open={open} onClose={handleClose} maxWidth="md">
      <DialogTitle>
        Create New Session
        <IconButton
          onClick={handleClose}
          sx={{ position: "absolute", right: 8, top: 8 }}
        >
          <X />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 3, mt: 2 }}>
          {/* Location Selection */}
          <FormControl fullWidth>
            <InputLabel>Select Location *</InputLabel>
            <Select
              value={selectedLocation}
              onChange={(e) => setSelectedLocation(e.target.value)}
              label="Select Location *"
            >
              {availableLocations.map((location) => (
                <MenuItem
                  key={location.location_Id}
                  value={location.location_Id}
                >
                  {location.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {/* Menu Selection */}
          <FormControl fullWidth>
            <InputLabel>Select Menu *</InputLabel>
            <Select
              value={selectedMenu}
              onChange={(e) => setSelectedMenu(e.target.value)}
              label="Select Menu *"
            >
              {availableMenus.map((menu) => (
                <MenuItem key={menu.menu_id} value={menu.menu_id}>
                  {menu.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {/* Table Assignment Section */}
          {selectedLocation && (
            <Box
              sx={{ border: 1, borderColor: "divider", borderRadius: 1, p: 2 }}
            >
              <FormLabel component="legend" sx={{ mb: 2 }}>
                Table Assignment
              </FormLabel>

              <RadioGroup
                value={tableAssignmentType}
                onChange={handleTableAssignmentTypeChange}
              >
                {/* <FormControlLabel
                  value="none"
                  control={<Radio />}
                  label="Assign Later"
                /> */}
                <FormControlLabel
                  value="table"
                  control={<Radio />}
                  label="Assign Individual Table"
                />
                <FormControlLabel
                  value="tableGroup"
                  control={<Radio />}
                  label="Assign Table Group"
                />
              </RadioGroup>

              {/* Individual Table Selection */}
              {tableAssignmentType === "table" && (
                <FormControl fullWidth sx={{ mt: 2 }}>
                  <InputLabel>Select Table</InputLabel>
                  <Select
                    value={selectedTable}
                    onChange={(e) => setSelectedTable(e.target.value)}
                    label="Select Table"
                  >
                    {availableTables.length === 0 ? (
                      <MenuItem disabled>No available tables</MenuItem>
                    ) : (
                      availableTables
                        .filter((table) => table.tableGroup_Id === null)
                        .map((table) => (
                          <MenuItem key={table.table_Id} value={table.table_Id}>
                            <Box
                              sx={{
                                display: "flex",
                                alignItems: "center",
                                gap: 1,
                              }}
                            >
                              Table {table.table_number}
                              <Chip
                                label={`${table.seat_count} seats`}
                                size="small"
                                variant="outlined"
                              />
                              {table.tableGroup_Name && (
                                <Chip
                                  label={table.tableGroup_Name}
                                  size="small"
                                  color="primary"
                                  variant="outlined"
                                />
                              )}
                            </Box>
                          </MenuItem>
                        ))
                    )}
                  </Select>
                  {availableTables.length === 0 && (
                    <Typography
                      variant="caption"
                      color="text.secondary"
                      sx={{ mt: 1 }}
                    >
                      All tables at this location are currently in use or
                      inactive
                    </Typography>
                  )}
                </FormControl>
              )}

              {/* Table Group Selection */}
              {tableAssignmentType === "tableGroup" && (
                <FormControl fullWidth sx={{ mt: 2 }}>
                  <InputLabel>Select Table Group</InputLabel>
                  <Select
                    value={selectedTableGroup}
                    onChange={(e) => setSelectedTableGroup(e.target.value)}
                    label="Select Table Group"
                  >
                    {availableTableGroups.length === 0 ? (
                      <MenuItem disabled>No available table groups</MenuItem>
                    ) : (
                      availableTableGroups.map((group) => (
                        <MenuItem
                          key={group.tableGroup_Id}
                          value={group.tableGroup_Id}
                        >
                          <Box
                            sx={{
                              display: "flex",
                              flexDirection: "column",
                              gap: 0.5,
                            }}
                          >
                            <Typography variant="body2">
                              {group.group_Name}
                            </Typography>
                            <Box sx={{ display: "flex", gap: 1 }}>
                              <Chip
                                label={`${group.table_Count} tables`}
                                size="small"
                                variant="outlined"
                              />
                              <Chip
                                label={`${group.total_Seats} seats`}
                                size="small"
                                variant="outlined"
                              />
                            </Box>
                          </Box>
                        </MenuItem>
                      ))
                    )}
                  </Select>
                  {availableTableGroups.length === 0 && (
                    <Typography
                      variant="caption"
                      color="text.secondary"
                      sx={{ mt: 1 }}
                    >
                      All table groups at this location are currently in use or
                      inactive
                    </Typography>
                  )}
                </FormControl>
              )}
            </Box>
          )}
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={loading}>
          Cancel
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={
            !selectedMenu ||
            !selectedLocation ||
            loading ||
            (tableAssignmentType === "table" && !selectedTable) ||
            (tableAssignmentType === "tableGroup" && !selectedTableGroup)
          }
        >
          {loading ? "Creating..." : "Create Session"}
        </Button>
      </DialogActions>
    </AppDialog>
  );
};

export default NewSessionDialog;
