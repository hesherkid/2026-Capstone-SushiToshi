import { useState, useEffect } from "react";
import {
  Box,
  DialogContent,
  DialogActions,
  Button,
  List,
  ListItem,
  ListItemText,
  IconButton,
  CircularProgress,
  Alert,
} from "@mui/material";
import { Trash, Plus } from "lucide-react";
import DialogHeader from "@/components/common/DialogHeader";
import serverMessage from "@/utils/serverMessage";

// Assign menus to a location or remove them. Shown inside the page's AppDialog
const MenuAssignmentForm = ({ location, onClose, api }) => {
  const [menus, setMenus] = useState([]);
  const [assignedMenus, setAssignedMenus] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!location) return;
    setLoading(true);
    setError(null);
    Promise.all([api.get("/Menu"), api.get(`/Location/${location.location_Id}/menus`)])
      .then(([allMenusRes, assignedMenusRes]) => {
        setMenus(allMenusRes.data);
        setAssignedMenus(assignedMenusRes.data);
      })
      .catch((err) => setError(serverMessage(err, "Couldn't load the menus.")))
      .finally(() => setLoading(false));
  }, [location]);

  const handleAssign = async (menuId) => {
    setError(null);
    try {
      await api.post(`/Location/${location.location_Id}/menus`, { menu_id: menuId });
      setAssignedMenus((prev) => [...prev, menuId]);
    } catch (err) {
      setError(serverMessage(err, "Couldn't assign the menu."));
    }
  };

  const handleUnassign = async (menuId) => {
    setError(null);
    try {
      await api.delete(`/Location/${location.location_Id}/menus/${menuId}`);
      setAssignedMenus((prev) => prev.filter((id) => id !== menuId));
    } catch (err) {
      setError(serverMessage(err, "Couldn't remove the menu."));
    }
  };

  return (
    <>
      <DialogHeader title="Manage Menus" subtitle={location?.name} onClose={onClose} />
      <DialogContent dividers>
        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {loading ? (
          <Box display="flex" justifyContent="center" p={3}>
            <CircularProgress />
          </Box>
        ) : (
          <List>
            {menus.map((menu) => {
              const assigned = assignedMenus.includes(menu.menu_id);
              return (
                <ListItem
                  key={menu.menu_id}
                  secondaryAction={
                    assigned ? (
                      <IconButton
                        edge="end"
                        aria-label={`Remove ${menu.name}`}
                        onClick={() => handleUnassign(menu.menu_id)}
                        color="error"
                      >
                        <Trash size={20} />
                      </IconButton>
                    ) : (
                      <IconButton
                        edge="end"
                        aria-label={`Assign ${menu.name}`}
                        onClick={() => handleAssign(menu.menu_id)}
                        color="primary"
                      >
                        <Plus size={20} />
                      </IconButton>
                    )
                  }
                >
                  <ListItemText primary={menu.name} />
                </ListItem>
              );
            })}
          </List>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </>
  );
};

export default MenuAssignmentForm;
