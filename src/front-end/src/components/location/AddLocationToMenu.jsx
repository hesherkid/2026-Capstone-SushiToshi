import { useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, DialogContent, DialogActions,
  Stack, FormControl, InputLabel, Select, MenuItem, Chip,
  Typography, Divider
} from "@mui/material";
import { X } from "lucide-react";
import api from "@/config/api";
import DialogHeader from "@/components/common/DialogHeader";
import serverMessage from "@/utils/serverMessage";

// Each add or remove saves straight away, so the dialog only needs Close.
// Shown inside the page's AppDialog
const AddLocationsToMenu = ({ menu, onClose, availableLocations = [] }) => {
  // Normalize locations to stable { id, name }
  const safeLocations = useMemo(() => {
    const arr = Array.isArray(availableLocations) ? availableLocations : [];
    return arr
      .map((loc) => ({
        id: String(loc?.location_Id ?? loc?.location_id ?? ""), // tolerate either casing
        name: loc?.name ?? "",
      }))
      .filter((x) => x.id && x.name);
  }, [availableLocations]);

  // Fast name lookup
  const nameById = useMemo(() => {
    const map = new Map();
    safeLocations.forEach((x) => map.set(x.id, x.name));
    return map;
  }, [safeLocations]);

  // Assigned = array of string ids derived from menu.menuLocations[]
  const [assignedLocations, setAssignedLocations] = useState(
    (menu?.menuLocations ?? [])
      .map((ml) => String(ml?.location_Id))
      .filter(Boolean)
  );

  const [selectedLocation, setSelectedLocation] = useState(""); // always a string
  const [error, setError] = useState(null);

  // Keep state in sync when the menu changes
  useEffect(() => {
    setAssignedLocations(
      (menu?.menuLocations ?? [])
        .map((ml) => String(ml?.location_Id))
        .filter(Boolean)
    );
    setSelectedLocation("");
    setError(null);
  }, [menu?.menu_id]);

  // Locations not yet assigned
  const unassignedLocations = useMemo(
    () => safeLocations.filter((x) => !assignedLocations.includes(x.id)),
    [safeLocations, assignedLocations]
  );

  // If selection becomes invalid after filtering, clear it
  useEffect(() => {
    if (selectedLocation && !unassignedLocations.some((x) => x.id === selectedLocation)) {
      setSelectedLocation("");
    }
  }, [unassignedLocations, selectedLocation]);

  const getLocationName = (id) => nameById.get(String(id)) ?? "Unknown";

  const handleAddLocation = async () => {
    const locId = selectedLocation ?? "";
    if (!locId) return;
    // optimistic add (revert on error)
    const revertNeeded = !assignedLocations.includes(locId);
    if (revertNeeded) setAssignedLocations((prev) => [...prev, locId]);
    setError(null);
    try {
      await api.put(`MenuLocation/menu/${menu.menu_id}/location/${locId}`);
    } catch (err) {
      console.error("PUT failed:", err);
      setError(serverMessage(err, `Couldn't add ${getLocationName(locId)}.`));
      if (revertNeeded) {
        setAssignedLocations((prev) => prev.filter((x) => x !== locId));
      }
    } finally {
      setSelectedLocation("");
    }
  };

  const handleRemoveLocation = async (locationId) => {
    const id = String(locationId);
    // optimistic remove (revert on error)
    const prev = assignedLocations;
    setAssignedLocations((curr) => curr.filter((x) => x !== id));
    if (selectedLocation === id) setSelectedLocation("");
    setError(null);

    try {
      await api.delete(`MenuLocation/menu/${menu.menu_id}/location/${id}`);
    } catch (err) {
      console.error("DELETE failed:", err);
      setError(serverMessage(err, `Couldn't remove ${getLocationName(id)}.`));
      setAssignedLocations(prev); // revert
    }
  };

  return (
    <>
      <DialogHeader title="Manage Locations" subtitle={menu?.name} onClose={onClose} />

      <DialogContent dividers>
        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        <Stack spacing={3}>
          {/* Assigned */}
          <Box>
            <Typography variant="subtitle2" gutterBottom sx={{ fontWeight: 600 }}>
              Assigned Locations
            </Typography>

            {assignedLocations.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{ fontStyle: "italic", py: 2 }}>
                No locations assigned yet
              </Typography>
            ) : (
              <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1, mt: 1 }}>
                {assignedLocations.map((locationId) => (
                  <Chip
                    key={locationId}
                    label={getLocationName(locationId)}
                    onDelete={() => handleRemoveLocation(locationId)}
                    deleteIcon={<X size={16} />}
                    color="primary"
                    variant="outlined"
                  />
                ))}
              </Box>
            )}
          </Box>

          <Divider />

          {/* Add */}
          <Box>
            <Typography variant="subtitle2" gutterBottom sx={{ fontWeight: 600 }}>
              Add Location
            </Typography>

            <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
              <FormControl fullWidth size="small">
                <InputLabel id="location-select-label">Select Location</InputLabel>
                <Select
                  labelId="location-select-label"
                  label="Select Location"
                  disabled={unassignedLocations.length === 0}
                  value={selectedLocation ?? ""}           
                  onChange={(e) => setSelectedLocation(String(e?.target?.value ?? ""))}
                  displayEmpty
                  renderValue={(val) =>
                    val
                      ? getLocationName(val)
                      : <em>{unassignedLocations.length === 0 ? "" : ""}</em>
                  }
                >
                  <MenuItem value="">
                    <em>{unassignedLocations.length === 0 ? "" : ""}</em>
                  </MenuItem>

                  {unassignedLocations.map((loc) => (
                    <MenuItem key={loc.id} value={loc.id}>
                      {loc.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <Button
                variant="outlined"
                onClick={handleAddLocation}
                disabled={!selectedLocation}
                sx={{ minWidth: "auto", px: 3 }}
              >
                Add
              </Button>
            </Stack>
          </Box>
        </Stack>
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </>
  );
};

export default AddLocationsToMenu;
