import { useState, useEffect } from "react";
import {
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Typography,
} from "@mui/material";
import { Edit, Menu, AlertTriangle, Trash2 } from "lucide-react";
import LocationForm from "@/components/location/LocationForm";
import MenuAssignmentForm from "@/components/location/MenuAssignmentForm";
import api from "@/config/api";
import ManagementPage from "@/components/common/ManagementPage";
import { actionsColumn } from "@/components/common/RowActions";

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
          phone_Number: r.phone_Number ?? r.phoneNumber,
          created_At: r.created_At ?? r.createdAt ?? r.CreatedAt,
        };
      });
      
      setLocations(normalized);
    } catch (err) {
      console.error("Error fetching data:", err);
      if (err.response) {
        const { status, data } = err.response;
        console.log("Status", status)
        console.log("Data", err.response.data)
        
        switch (status) {
          case 401:
            setError("Unauthorized. Please log in again.");
            router.push("/auth/login");
            break;
          case 409:
            setError(err.response.data || "Conflict error occurred");
            console.log("Conflict message:", err.response.data);
            break;
          case 404:
            setError(err.response.data || "Resource not found");
            break;
          case 500:
            setError(err.response.data || "Server error occurred");
            break;
          default:
            setError(err.response.data || "An error occurred");
        }
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchLocations();
  }, []);

  const handleCreate = () => {
    setSelectedLocation(null);
    setFormMode("create");
    setIsFormOpen(true);
  };

  const handleEdit = (location) => {
    setSelectedLocation(location);
    setFormMode("edit");
    setIsFormOpen(true);
  };

  const handleDeleteClick = (location) => {
    setLocationToDelete(location);
    setDeleteDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    try {
      await api.delete(`/Location/${locationToDelete.location_Id}`);
      await fetchLocations();
      setDeleteDialogOpen(false);
      setLocationToDelete(null);
    } catch (err) {
      console.error("Error fetching data:", err);
      if (err.response) {
        const { status, data } = err.response;
        console.log("Status", status)
        console.log("Data", err.response.data)
        
        switch (status) {
          case 401:
            setError("Unauthorized. Please log in again.");
            router.push("/auth/login");
            break;
          case 409:
            setError(err.response.data || "Conflict error occurred");
            console.log("Conflict message:", err.response.data);
            break;
          case 404:
            setError(err.response.data || "Resource not found");
            break;
          case 500:
            setError(err.response.data || "Server error occurred");
            break;
          default:
            setError(err.response.data || "An error occurred");
        }
      }
    }
  };

  const handleSubmit = async (formData) => {
    try {
      if (formMode === "create") {
        await api.post("/Location", formData);
      } else {
        // TODO: Need endpoint to /Location/{id} on the backend
        await api.put(`/Location/${selectedLocation.location_Id}`,
          formData
        );
      }
      await fetchLocations();
      setIsFormOpen(false);
    } catch (err) {
      console.error("Error fetching data:", err);
      if (err.response) {
        const { status, data } = err.response;
        console.log("Status", status)
        console.log("Data", err.response.data)
        
        switch (status) {
          case 401:
            setError("Unauthorized. Please log in again.");
            router.push("/auth/login");
            break;
          case 409:
            setError(err.response.data || "Conflict error occurred");
            console.log("Conflict message:", err.response.data);
            break;
          case 404:
            setError(err.response.data || "Resource not found");
            break;
          case 500:
            setError(err.response.data || "Server error occurred");
            break;
          default:
            setError(err.response.data || "An error occurred");
        }
      }
    }
  };


  const columns = [
    { field: "location_Id", headerName: "ID", width: 90 },
    { field: "name", headerName: "Name", width: 200 },
    { field: "address_one", headerName: "Address 1", width: 200 },
    actionsColumn((row) => [
      { label: "Edit", icon: Edit, onClick: () => handleEdit(row) },
      {
        label: "Delete",
        icon: Trash2,
        color: "error",
        onClick: () => handleDeleteClick(row),
      },
      {
        label: "Assign Menus",
        icon: Menu,
        color: "primary",
        onClick: () => {
          setSelectedLocation(row);
          setIsMenuFormOpen(true);
        },
      },
    ]),
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
      }}
    >

      <Dialog
        open={isFormOpen}
        onClose={() => setIsFormOpen(false)}
        maxWidth="sm"
        fullWidth
      >
        <LocationForm
          initialData={selectedLocation}
          onSubmit={handleSubmit}
          onClose={() => setIsFormOpen(false)}
          mode={formMode}
        />
      </Dialog>

      <Dialog
        open={isMenuFormOpen}
        onClose={() => setIsMenuFormOpen(false)}
        maxWidth="sm"
        fullWidth
      >
        <MenuAssignmentForm
          location={selectedLocation}
          onClose={() => setIsMenuFormOpen(false)}
          api={api}
        />
      </Dialog>

      <Dialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        maxWidth="xs"
        fullWidth
      >
        <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <AlertTriangle color="error" size={24} />
          Confirm Delete Location
        </DialogTitle>
        <DialogContent>
          <Typography>
            Are you sure you want to delete the location "
            {locationToDelete?.name}"? This will remove all menu assignments for
            this location. This action cannot be undone.
          </Typography>
        </DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button onClick={() => setDeleteDialogOpen(false)} variant="outlined">
            Cancel
          </Button>
          <Button
            onClick={handleDeleteConfirm}
            variant="contained"
            color="error"
            autoFocus
          >
            Delete Location
          </Button>
        </DialogActions>
      </Dialog>
    </ManagementPage>
  );
};

export default LocationManagement;
