import { useState, useEffect } from "react";
import {
  Button,
  DialogTitle,
  DialogContent,
  DialogActions,
  Typography,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { Edit, AlertTriangle, Trash2, LocateFixed } from "lucide-react";
import MenuForm from "@/components/location/MenuForm";
import AddLocationsToMenu from "@/components/location/AddLocationToMenu";
import api from "@/config/api";
import ManagementPage from "@/components/common/ManagementPage";
import { actionsColumn } from "@/components/common/RowActions";

const MenuManagement = () => {
  const [menus, setMenus] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedMenu, setSelectedMenu] = useState(null);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [formMode, setFormMode] = useState("create");
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [menuToDelete, setMenuToDelete] = useState(null);
  const [locationDialogOpen, setLocationDialogOpen] = useState(false);
  const [locationsList, setLocationsList] = useState([]);


  const fetchMenus = async () => {
    try {
      const response = await api.get("/Menu");
      setMenus(response.data);
      console.log(response.data)
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
            console.error("Conflict message:", err.response.data);
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



  const fetchLocations = async () => {
    try{
      const response = await api.get("/location")
      setLocationsList(response.data);
    }
    catch (error){
      console.error("Error fetching data:", err);
      if (err.response) {
        const { status, data } = err.response;
        
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

  useEffect(() => {
    fetchMenus();
    fetchLocations();
  }, []);


  const handleCreate = () => {
    setSelectedMenu(null);
    setFormMode("create");
    setIsFormOpen(true);
  };

  const handleLocations = (menu) => {
      setSelectedMenu(menu);
  setLocationDialogOpen(true);
  };

  const handleEdit = (menu) => {
    setSelectedMenu(menu);
    setFormMode("edit");
    setIsFormOpen(true);
  };

  const handleDeleteClick = (menu) => {
    setMenuToDelete(menu);
    setDeleteDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    try {
      await api.delete(`/Menu/${menuToDelete.menu_id}`);
      await fetchMenus();
      setDeleteDialogOpen(false);
      setMenuToDelete(null);
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
      console.error(err);
    }
  };

  const handleSubmit = async (formData) => {
    try {
      if (formMode === "create") {
        await api.post("/Menu/createmenu", formData);
      } else {
        await api.put(`/Menu/${selectedMenu.menu_id}`,
          formData
        );
      }
      await fetchMenus();
      setIsFormOpen(false);
    } catch (err) {
      console.error("Error fetching data:", err);
      if (err.response) {
        const { status, data } = err.response;

        
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
      console.error(err);
    }
  };

  const columns = [
    { field: "menu_id", headerName: "ID", width: 90 },
    { field: "name", headerName: "Name", width: 200 },
    { field: "description", headerName: "Description", width: 300 },
    { 
      field: "is_active",
      headerName: "Is Active",
      width: 100,
      renderCell: (params) => (
        <Typography>
          {params.value ? "Yes" : "No"}
        </Typography>
      )
    },
    actionsColumn((row) => [
      { label: "Edit", icon: Edit, onClick: () => handleEdit(row) },
      {
        label: "Delete",
        icon: Trash2,
        color: "error",
        onClick: () => handleDeleteClick(row),
      },
      {
        label: "Assign Locations",
        icon: LocateFixed,
        onClick: () => handleLocations(row),
      },
    ]),
  ];

  return (
    <ManagementPage
      title="Menu Management"
      addLabel="Add New Menu Type"
      onAdd={handleCreate}
      error={error}
      onErrorClose={() => setError(null)}
      gridProps={{
        rows: menus,
        columns,
        loading,
        getRowId: (row) => row.menu_id,
      }}
    >
      <AppDialog
        open={isFormOpen}
        onClose={() => setIsFormOpen(false)}
      >
        <MenuForm
          initialData={selectedMenu}
          onSubmit={handleSubmit}
          onClose={() => setIsFormOpen(false)}
          mode={formMode}
        />
      </AppDialog>

      <AppDialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        maxWidth="xs"
      >
        <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <AlertTriangle color="error" size={24} />
          Confirm Delete
        </DialogTitle>
        <DialogContent>
          <Typography>
            Are you sure you want to delete the menu "{menuToDelete?.name}"?
            This action cannot be undone.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteDialogOpen(false)} variant="outlined">
            Cancel
          </Button>
          <Button
            onClick={handleDeleteConfirm}
            variant="contained"
            color="error"
            autoFocus
          >
            Delete Menu
          </Button>
        </DialogActions>
      </AppDialog>
      <AppDialog open={locationDialogOpen} onClose={() => setLocationDialogOpen(false)}>
        <AddLocationsToMenu
          menu={selectedMenu}
          // onSubmit={handleLocationSubmit}
          onClose={() => setLocationDialogOpen(false)}
          availableLocations={locationsList}
        />
      </AppDialog>
    </ManagementPage>
  );
};

export default MenuManagement;
