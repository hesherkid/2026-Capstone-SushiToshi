import { useState, useEffect } from "react";
import { Box, Chip } from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import serverMessage from "@/utils/serverMessage";
import LocationForm from "@/components/location/LocationForm";
import MenuAssignmentForm from "@/components/location/MenuAssignmentForm";
import api from "@/config/api";
import ManagementPage from "@/components/common/ManagementPage";
import { actionsColumn } from "@/components/common/RowActions";
import { Edit, Trash2, DollarSign } from "lucide-react";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import LocationPricingDialog from "@/components/location/LocationPricingDialog";
import { getLocationPricingList } from "@/services/pricingService";

// The menu book customers see on their Menu tab; RowActions passes it a size like a Lucide icon
const MenuBookIcon = ({ size }) => <MaterialSymbol name="menu_book_2" size={size} />;

const LocationManagement = () => {
  const [locations, setLocations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedLocation, setSelectedLocation] = useState(null);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isMenuFormOpen, setIsMenuFormOpen] = useState(false);
  const [formMode, setFormMode] = useState("create");
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [locationToDelete, setLocationToDelete] = useState(null);
  const [pricingLocation, setPricingLocation] = useState(null);
  // Shown inside the location form and the delete confirmation
  const [formError, setFormError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [deleteError, setDeleteError] = useState(null);
  // location id -> names of its menus; null until loaded (or if they couldn't be)
  const [menuNames, setMenuNames] = useState(null);
  // location id -> "Default" or "Location"; null until loaded (or if it couldn't be)
  const [pricingSources, setPricingSources] = useState(null);

  // Only for the Pricing column, so a failure leaves that column blank rather than showing an error
  const fetchPricingSources = async () => {
    try {
      const list = await getLocationPricingList();
      setPricingSources(Object.fromEntries(list.map((l) => [l.location_id, l.source])));
    } catch (err) {
      console.error("Error fetching pricing:", err);
      setPricingSources(null);
    }
  };


  const fetchLocations = async () => {
    try {
      setLoading(true);
      const response = await api.get("/Location");
      const data = Array.isArray(response.data) ? response.data : [];

      const normalized = data.map((r) => {
        const locationId = r.location_Id ?? r.location_id ?? r.locationId ?? r.id ?? r.ID ?? null;

        return {
          ...r,
          location_Id: locationId,
          id: locationId, // DataGrid default id field
          name: r.name ?? r.Name,
          address_one: r.address_one ?? r.address_Primary ?? r.addressPrimary ?? r.address1,
          address_two: r.address_two ?? r.address_Secondary ?? r.addressSecondary ?? r.address2,
          city: r.city ?? r.City,
          province: r.province ?? r.Province,
          postal_code: r.postal_code ?? r.postalCode ?? r.postal_Code ?? r.PostalCode,
          province_code: r.province_code ?? r.province_Code ?? "AB",
          phone_Number: r.phone_Number ?? r.phoneNumber,
          created_At: r.created_At ?? r.createdAt ?? r.CreatedAt,
        };
      });
      
      setLocations(normalized);
    } catch (err) {
      console.error("Error fetching data:", err);
      setError(serverMessage(err, "Couldn't load the locations."));
    } finally {
      setLoading(false);
    }
  };

  // Only for the Menus column, so like pricing a failure leaves it blank.
  // The API gives each location's menu ids, so the names come from the full menu list
  const fetchMenuNames = async (locationList) => {
    try {
      const [allMenus, ...assigned] = await Promise.all([
        api.get("/Menu"),
        ...locationList.map((l) => api.get(`/Location/${l.location_Id}/menus`)),
      ]);
      const nameById = Object.fromEntries((allMenus.data ?? []).map((m) => [m.menu_id, m.name]));
      setMenuNames(
        Object.fromEntries(
          locationList.map((l, i) => [
            l.location_Id,
            (assigned[i].data ?? []).map((id) => nameById[id]).filter(Boolean),
          ]),
        ),
      );
    } catch (err) {
      console.error("Error fetching menus:", err);
      setMenuNames(null);
    }
  };

  useEffect(() => {
    fetchLocations();
    fetchPricingSources();
  }, []);

  useEffect(() => {
    if (locations.length > 0) fetchMenuNames(locations);
  }, [locations]);

  const closeMenuForm = () => {
    setIsMenuFormOpen(false);
    fetchMenuNames(locations);   // menus may have been assigned or removed
  };

  const handleCreate = () => {
    setSelectedLocation(null);
    setFormMode("create");
    setFormError(null);
    setIsFormOpen(true);
  };

  const handleEdit = (location) => {
    setSelectedLocation(location);
    setFormMode("edit");
    setFormError(null);
    setIsFormOpen(true);
  };

  const handleDeleteClick = (location) => {
    setLocationToDelete(location);
    setDeleteError(null);
    setDeleteDialogOpen(true);
  };

  // Returns false on failure, so the confirm dialog stays open and shows why
  const handleDeleteConfirm = async () => {
    setDeleteError(null);
    try {
      await api.delete(`/Location/${locationToDelete.location_Id}`);
      await fetchLocations();
      setLocationToDelete(null);
    } catch (err) {
      setDeleteError(serverMessage(err, "Couldn't delete the location."));
      return false;
    }
  };

  const handleSubmit = async (formData) => {
    setSaving(true);
    setFormError(null);
    try {
      if (formMode === "create") {
        await api.post("/Location", formData);
      } else {
        await api.put(`/Location/${selectedLocation.location_Id}`, formData);
      }
      await fetchLocations();
      setIsFormOpen(false);
    } catch (err) {
      setFormError(serverMessage(err, "Couldn't save the location."));
    } finally {
      setSaving(false);
    }
  };

  const columns = [
    { field: "location_Id", headerName: "ID", width: 90 },
    // Name, address and menus share the spare width, so no one column ends in a wide gap
    { field: "name", headerName: "Name", flex: 1, minWidth: 160 },
    { field: "address_one", headerName: "Address 1", flex: 1.2, minWidth: 180 },
    {
      field: "menus",
      headerName: "Menus",
      flex: 0.8,
      minWidth: 140,
      sortable: false,
      // Joined so the quick search and filters can match a menu's name
      valueGetter: (value, row) => menuNames && (menuNames[row.location_Id] ?? []).join(", "),
      renderCell: ({ row }) => {
        if (!menuNames) return null;
        const names = menuNames[row.location_Id] ?? [];
        if (names.length === 0) return <Chip label="No menus" size="small" color="warning" variant="outlined" />;
        // One per line; the row grows to fit (see getRowHeight below)
        return (
          <Box sx={{ display: "flex", flexDirection: "column", alignItems: "flex-start", gap: 0.5, py: 1 }}>
            {names.map((name, i) => (
              <Chip key={`${name}-${i}`} label={name} size="small" variant="outlined" />
            ))}
          </Box>
        );
      },
    },
    {
      field: "pricing",
      headerName: "Pricing",
      width: 130,
      // A location missing from the list is new, and new locations use the default
      valueGetter: (value, row) =>
        pricingSources && (pricingSources[row.location_Id] === "Location" ? "Custom" : "Default"),
      renderCell: ({ value, row }) =>
        value && (
          <Chip
            label={value}
            size="small"
            color={value === "Custom" ? "primary" : "default"}
            variant={value === "Custom" ? "filled" : "outlined"}
            onClick={() => setPricingLocation(row)}
            aria-label={`${value} pricing for ${row.name}`}
          />
        ),
    },
    actionsColumn((row) => [
      { label: "Edit", icon: Edit, onClick: () => handleEdit(row) },
      {
        label: "Assign Menus",
        icon: MenuBookIcon,
        onClick: () => {
          setSelectedLocation(row);
          setIsMenuFormOpen(true);
        },
      },
      {
        label: "Pricing",
        icon: DollarSign,
        onClick: () => setPricingLocation(row),
      },
      {
        label: "Delete",
        icon: Trash2,
        color: "error",
        onClick: () => handleDeleteClick(row),
      },
    ], { width: 190 }),   // four icons need more than the default 150, or Delete touches the border
  ];

  return (
    <ManagementPage
      title="Location Management"
      addLabel="Add New Location"
      onAdd={handleCreate}
      error={error}
      onErrorClose={() => setError(null)}
      gridProps={{
        rows: locations,
        columns,
        loading,
        getRowId: (row) => row.location_Id,
        // Taller rows for locations with several menus
        getRowHeight: () => "auto",
        sx: { "& .MuiDataGrid-cell": { minHeight: 52 } },
      }}
    >

      {/* Mounted only while open, so the fields start from the chosen location each time */}
      {isFormOpen && (
        <LocationForm
          initialData={selectedLocation}
          onSubmit={handleSubmit}
          onClose={() => setIsFormOpen(false)}
          mode={formMode}
          busy={saving}
          error={formError}
          onErrorClose={() => setFormError(null)}
        />
      )}

      <AppDialog
        open={isMenuFormOpen}
        onClose={closeMenuForm}
      >
        <MenuAssignmentForm
          location={selectedLocation}
          onClose={closeMenuForm}
          api={api}
        />
      </AppDialog>

      <LocationPricingDialog
        open={!!pricingLocation}
        location={pricingLocation}
        onClose={() => {
          setPricingLocation(null);
          fetchPricingSources();   // custom pricing may have been switched on or off
        }}
      />

      <ConfirmDialog
        open={deleteDialogOpen}
        title="Delete location"
        message={`Are you sure you want to delete the location "${locationToDelete?.name}"? This will remove all menu assignments for this location. This action cannot be undone.`}
        confirmLabel="Delete Location"
        error={deleteError}
        onConfirm={handleDeleteConfirm}
        onClose={() => setDeleteDialogOpen(false)}
      />
    </ManagementPage>
  );
};

export default LocationManagement;
