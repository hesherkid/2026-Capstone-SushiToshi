import { useState, useEffect } from "react";
import {
  Box,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  IconButton,
  CircularProgress,
  Alert,
} from "@mui/material";
import { Trash, Plus } from "lucide-react";

const MenuAssignmentForm = ({ location, onClose, api }) => {
  const [menus, setMenus] = useState([]);
  const [assignedMenus, setAssignedMenus] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [allMenusRes, assignedMenusRes] = await Promise.all([
          api.get("/Menu"),
          // TODO: Endpoint to get menus assigned to a location (old project api: /locations/{locationId}/menus)
          api.get(`/Location/${location.location_Id}/menus`),
        ]);

        setMenus(allMenusRes.data);
        setAssignedMenus(assignedMenusRes.data);
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

    fetchData();
  }, [location]);

  const handleAssign = async (menuId) => {
    try {
      // old project api: /locations/{locationId}/menus
      await api.post(`/Location/${location.location_Id}/menus`, {
        menu_id: menuId,
      });
      setAssignedMenus([...assignedMenus, menuId]);
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

  const handleUnassign = async (menuId) => {
    try {
      // old project api: /locations/{locationId}/menus/{menuId}
      await api.delete(
        `/Location/${location.location_Id}/menus/${menuId}`
      );
      setAssignedMenus(assignedMenus.filter((id) => id !== menuId));
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

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" p={3}>
        <CircularProgress />
      </Box>
    );
  }

  // TODO: Fix deprecated ListItemSecondaryAction warning
  return (
    <>
      <DialogTitle>Manage Menus - {location.name}</DialogTitle>
      <DialogContent>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <List>
          {menus.map((menu) => (
            <ListItem key={menu.menu_id}>
              <ListItemText primary={menu.name} />
              <ListItemSecondaryAction>
                {assignedMenus.includes(menu.menu_id) ? (
                  <IconButton
                    edge="end"
                    onClick={() => handleUnassign(menu.menu_id)}
                    color="error"
                  >
                    <Trash size={20} />
                  </IconButton>
                ) : (
                  <IconButton
                    edge="end"
                    onClick={() => handleAssign(menu.menu_id)}
                    color="primary"
                  >
                    <Plus size={20} />
                  </IconButton>
                )}
              </ListItemSecondaryAction>
            </ListItem>
          ))}
        </List>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </>
  );
};

export default MenuAssignmentForm;
