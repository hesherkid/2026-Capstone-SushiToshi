import { useState, useEffect } from "react";
import { DataGrid, gridRenderContextColumnsSelector } from "@mui/x-data-grid";
import {
  Box,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  IconButton,
  Typography,
  Stack,
  Alert,
  FormControl
} from "@mui/material";
import { Edit, Delete, Plus, AlertTriangle, Trash2, LocateFixed  } from "lucide-react";
import { styled } from "@mui/material/styles";
import MenuForm from "@/components/location/MenuForm";
import AddLocationsToMenu from "@/components/location/AddLocationToMenu";
import api from "@/config/api";

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

  const AddButton = styled(IconButton)(({ theme }) => ({
    backgroundColor: theme.palette.primary.main,
    color: "white",
    padding: theme.spacing(1),
    minWidth: "auto",
    "&:hover": {
      backgroundColor: theme.palette.primary.dark,
    },
    "& svg": {
      width: 20,
      height: 20,
    },
  }));

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
    {
      field: "actions",
      headerName: "Actions",
      width: 150,
      renderCell: (params) => (
        <Stack direction="row" spacing={1}>
          <IconButton onClick={() => handleEdit(params.row)} size="small">
            <Edit size={20} />
          </IconButton>
          <IconButton
            onClick={() => handleDeleteClick(params.row)}
            size="small"
            color="error"
          >
            <Trash2 size={20} />
          </IconButton>
          <IconButton onClick={()=>handleLocations(params.row)} size ="small">
            <LocateFixed size ={20} />
          </IconButton>
        </Stack>
      ),
    },
  ];

  return (
    <Box pt={2}>
      <Stack direction="row" justifyContent="left" alignItems="center" mb={2}>
        <Typography pr={3} variant="h4">
          Menu Management
        </Typography>
        <AddButton onClick={handleCreate}>
          <Plus />
        </AddButton>
      </Stack>

      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <DataGrid
        rows={menus}
        columns={columns}
        loading={loading}
        getRowId={(row) => row.menu_id}
        autoHeight
        disableSelectionOnClick
        sx={{
          bgcolor: "background.paper",
          "& .MuiDataGrid-cell": {
            display: "flex",
            alignItems: "center",
          },
          "& .MuiDataGrid-cell:focus": {
            outline: "none",
          },
        }}
      />

      <Dialog
        open={isFormOpen}
        onClose={() => setIsFormOpen(false)}
        maxWidth="sm"
        fullWidth
      >
        <MenuForm
          initialData={selectedMenu}
          onSubmit={handleSubmit}
          onClose={() => setIsFormOpen(false)}
          mode={formMode}
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
          Confirm Delete
        </DialogTitle>
        <DialogContent>
          <Typography>
            Are you sure you want to delete the menu "{menuToDelete?.name}"?
            This action cannot be undone.
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
            Delete Menu
          </Button>
        </DialogActions>
      </Dialog>
      <Dialog open={locationDialogOpen} onClose={() => setLocationDialogOpen(false)}>
        <AddLocationsToMenu
          menu={selectedMenu}
          // onSubmit={handleLocationSubmit}
          onClose={() => setLocationDialogOpen(false)}
          availableLocations={locationsList}
        />
      </Dialog>
    </Box>
  );
};

export default MenuManagement;
