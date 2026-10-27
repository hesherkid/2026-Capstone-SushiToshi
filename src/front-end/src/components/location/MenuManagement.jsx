import { useState, useEffect } from "react";
import { Typography } from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import serverMessage from "@/utils/serverMessage";
import { Edit, Trash2, LocateFixed } from "lucide-react";
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
  // Shown inside the menu form and the delete confirmation
  const [formError, setFormError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [deleteError, setDeleteError] = useState(null);


  const fetchMenus = async () => {
    try {
      const response = await api.get("/Menu");
      setMenus(response.data);
    } catch (err) {
      console.error("Error fetching data:", err);
      setError(serverMessage(err, "Couldn't load the menus."));
    } finally {
      setLoading(false);
      
    }
  };



  const fetchLocations = async () => {
    try {
      const response = await api.get("/location");
      setLocationsList(response.data);
    } catch (err) {
      console.error("Error fetching data:", err);
      setError(serverMessage(err, "Couldn't load the locations."));
    }
  };

  useEffect(() => {
    fetchMenus();
    fetchLocations();
  }, []);


  const handleCreate = () => {
    setSelectedMenu(null);
    setFormMode("create");
    setFormError(null);
    setIsFormOpen(true);
  };

  const handleLocations = (menu) => {
      setSelectedMenu(menu);
  setLocationDialogOpen(true);
  };

  const handleEdit = (menu) => {
    setSelectedMenu(menu);
    setFormMode("edit");
    setFormError(null);
    setIsFormOpen(true);
  };

  const handleDeleteClick = (menu) => {
    setMenuToDelete(menu);
    setDeleteError(null);
    setDeleteDialogOpen(true);
  };

  // Returns false on failure, so the confirm dialog stays open and shows why
  const handleDeleteConfirm = async () => {
    setDeleteError(null);
    try {
      await api.delete(`/Menu/${menuToDelete.menu_id}`);
      await fetchMenus();
      setMenuToDelete(null);
    } catch (err) {
      setDeleteError(serverMessage(err, "Couldn't delete the menu."));
      return false;
    }
  };

  const handleSubmit = async (formData) => {
    setSaving(true);
    setFormError(null);
    try {
      if (formMode === "create") {
        await api.post("/Menu/createmenu", formData);
      } else {
        await api.put(`/Menu/${selectedMenu.menu_id}`, formData);
      }
      await fetchMenus();
      setIsFormOpen(false);
    } catch (err) {
      setFormError(serverMessage(err, "Couldn't save the menu."));
    } finally {
      setSaving(false);
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
      {/* Mounted only while open, so the fields start from the chosen menu each time */}
      {isFormOpen && (
        <MenuForm
          initialData={selectedMenu}
          onSubmit={handleSubmit}
          onClose={() => setIsFormOpen(false)}
          mode={formMode}
          busy={saving}
          error={formError}
          onErrorClose={() => setFormError(null)}
        />
      )}

      <ConfirmDialog
        open={deleteDialogOpen}
        title="Delete menu"
        message={`Are you sure you want to delete the menu "${menuToDelete?.name}"? This action cannot be undone.`}
        confirmLabel="Delete Menu"
        error={deleteError}
        onConfirm={handleDeleteConfirm}
        onClose={() => setDeleteDialogOpen(false)}
      />
      <AppDialog open={locationDialogOpen} onClose={() => setLocationDialogOpen(false)}>
        <AddLocationsToMenu
          menu={selectedMenu}
          onClose={() => setLocationDialogOpen(false)}
          availableLocations={locationsList}
        />
      </AppDialog>
    </ManagementPage>
  );
};

export default MenuManagement;
