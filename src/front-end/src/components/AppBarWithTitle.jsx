"use client";
import { useState, useEffect } from "react";
import {
  AppBar,
  Toolbar,
  Typography,
  Button,
  IconButton,
  Drawer,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  ListItemButton,
  Box,
  useTheme,
  CircularProgress,
  Select,
  MenuItem,
  FormControl,
} from "@mui/material";
import {
  Menu as MenuIcon,
  MenuSquare,
  LogOut,
  LogIn,
  Home,
  Clock,
  Users,
  BarChart,
  Tag,
  QrCode,
  Phone,
  MapPin,
  Table,
  Group,
} from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import Image from "next/image";
import { useAuth } from "@/hooks/useAuth";
import { logoutUser } from "@/utils/auth";
import { dispatchAuthChange } from "@/hooks/useAuth";
import { login, logout } from '@/utils/auth';
import api from "@/config/api";
import storage from "@/utils/storage";
import { LocationOn } from "@mui/icons-material";
import { useNotification } from "@/contexts/NotificationContext";

const AppBarWithTitle = ({ title }) => {
  const router = useRouter();
  const pathname = usePathname();
  const isLoginPage = pathname === "/auth/login";
  const theme = useTheme();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [selectedLocation, setSelectedLocation] = useState(
    storage.get("branch-location")
  );
  const [allLocations, setAllLocations] = useState([]);
  const { isAuthenticated, userRole, loading } = useAuth();
  const { notifySuccess, notifyError, notifyInfo } = useNotification(); 
  const isGuest = typeof window !== "undefined" && localStorage.getItem("guest") === "true";

  useEffect(() => {
    const fetchAllLocations = async () => {
      try {
        if (!isAuthenticated || !userRole || (userRole !== "staff" && userRole !== "admin")) {
          return;
        }
        const response = await api.get("location/");
        setAllLocations(response.data);

        // Set initial location if not already set
        const storedLocation = storage.get("branch-location");
        if (!storedLocation && response.data.length > 0) {
          const firstLocation = response.data[0].location_Id;
          setSelectedLocation(firstLocation);
          storage.set("branch-location", firstLocation);

          const locationName = response.data[0].name;
          notifyInfo(`Working location set to ${locationName}`);
        }
      } catch (err) {
        console.error("Error Fetching Locations", err);
        notifyError("Failed to load locations");
      }
    };
    fetchAllLocations();
  }, [isAuthenticated, userRole, loading, notifyError, notifyInfo]);

  // useEffect(() => {
  //   // Listen for auth changes
  //   const handleAuthEvent = () => {
  //     setDrawerOpen(false);
  //   };
  //   // Remove MSAL event, keep only Auth0 event
  //   window.addEventListener("auth:changed", handleAuthEvent);
  //   return () => {
  //     window.removeEventListener("auth:changed", handleAuthEvent);
  //   };
  // }, []);
  
const handleLogout = async () => {
  await logoutUser(true); // true = redirect to login
  setDrawerOpen(false);
};

  const handleLogin = () => {
    // Clear guest mode
    if (typeof window !== "undefined") {
      localStorage.removeItem("guest");
      localStorage.removeItem("session_id");
      localStorage.removeItem("guest_oid");
      localStorage.removeItem("tableNumber");
    }
    
    setDrawerOpen(false);
    router.push("/login");
  };
  const handleNavigation = (path) => {
    router.push(path);
    setDrawerOpen(false);
  };

  const handleLocationChange = async (event) => {
    const locationId = event.target.value;
    // console.log("Event: ", event.target.value);
    setSelectedLocation(locationId);
    storage.set("branch-location", locationId);
    window.location.reload();

    try {
      const response = await api.put(
      `staff/update-user-location/${locationId}`
      );
    } catch (err) {
      console.error("Error updating location:", err);
      // Optionally revert the selection on error
      // setSelectedLocation(previousValue);
    }
  };
  const menuItems = {
    customer: [
      { icon: Home, label: "Home", path: "/" },
      { icon: Clock, label: "Bills", path: "/dashboard/bills" },
      { icon: MenuSquare, label: "Menu", path: "/menu/full-menu" },
      { icon: Users, label: "Orders", path: "/dashboard/orders" },
      { icon: Phone, label: "Call Server", path: "/dashboard/call-server" },
    ],
    staff: [
      { icon: Home, label: "Home", path: "/" },
      { icon: Clock, label: "Sessions", path: "/dashboard/sessions" },
      { icon: Table, label: "Tables", path: "/dashboard/tables" },
      { icon: Users, label: "Orders", path: "/staff/dashboard/orders" },
      { icon: QrCode, label: "QR Codes", path: "/admin/qr-codes" },
    ],
    admin: [
      { icon: Home, label: "Home", path: "/" },
      { icon: Clock, label: "Sessions", path: "/dashboard/sessions" },
      { icon: Table, label: "Tables", path: "/dashboard/tables" },
      { icon: MapPin,label: "Manage Locations",path: "/location/menu-location",},
      { icon: Users, label: "Orders", path: "/staff/dashboard/orders" },
      { icon: MenuIcon, label: "Manage Menu Items", path: "/admin/menu-items" },
      { icon: Group, label: "Manage Menu Categories", path: "/admin/menu-categories" },
      { icon: Users, label: "Manage Staff", path: "/admin/staff" },
      { icon: Tag, label: "Manage Tags", path: "/tags/tag-management" },
      { icon: QrCode, label: "QR Codes", path: "/admin/qr-codes" },
      { icon: BarChart, label: "Analytics", path: "/analytics/analytic-page" },
    ],
  };

  const effectiveRole = userRole || (isGuest ? "customer" : null);
 console.log(effectiveRole)
  const renderMenuList = () => (
    <List
      sx={{
        width: 320,
        pt: 3,
      }}
    >
      {effectiveRole &&
        menuItems[effectiveRole].map((item, index) => {
          const Icon = item.icon;
          return (
            <ListItemButton
              key={index}
              onClick={() => handleNavigation(item.path)}
              sx={{
                cursor: "pointer",
                py: 3,
                "& .MuiListItemIcon-root": {
                  minWidth: 56,
                  "& svg": {
                    width: 24,
                    height: 24,
                  },
                },
                "& .MuiListItemText-primary": {
                  fontSize: "1.1rem",
                  fontWeight: 500,
                },
              }}
            >
              <ListItemIcon>
                <Icon />
              </ListItemIcon>
              <ListItemText primary={item.label} />
            </ListItemButton>
          );
        })}
    </List>
  );

  return (
    <AppBar
      position="fixed"
      sx={{ zIndex: theme.zIndex.drawer + 1, overflow: "visible" }}
    >
      <Toolbar
        sx={{
          minHeight: { xs: 56, sm: 64 },
          display: "flex",
          justifyContent: "space-between",
          px: { xs: 1, sm: 2 },
        }}
      >
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "start",
            gap: 1,
            flex: "1 1 0",
          }}
        >
          {(isAuthenticated  || isGuest) && (
            <IconButton
              color="inherit"
              aria-label="menu"
              onClick={() => setDrawerOpen(true)}
              sx={{ p: { xs: 1, sm: 1.5 } }}
            >
              <MenuIcon />
            </IconButton>
          )}
          <Typography
            variant="h6"
            component="div"
            sx={{
              fontSize: { xs: "1rem", sm: "1.25rem" },
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
            }}
          >
            {title}
          </Typography>
        </Box>

        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 2,
          }}
        >
          <Image
            src="/images/logo.png"
            alt="Logo"
            width={115}
            height={70}
            style={{
              objectFit: "contain",
              width: "clamp(80px, 20vw, 115px)",
              height: "auto",
              padding: "4px",
            }}
            priority
          />
        </Box>

        <Box
          sx={{
            flex: "1 1 0",
            display: "flex",
            justifyContent: "flex-end",
            alignItems: "center",
            gap: 2,
          }}
        >
          {/* Location Dropdown - Only visible for staff and admin */}
          {isAuthenticated &&
            (userRole === "staff" || userRole === "admin") && (
              <FormControl size="small">
                <Select
                  value={Number(selectedLocation)}
                  onChange={handleLocationChange}
                  renderValue={(value) => {
                    const location = allLocations?.find(
                      (loc) => loc.location_Id === value
                    );
                    return (
                      <Box
                        sx={{ display: "flex", alignItems: "center", gap: 1 }}
                      >
                        <LocationOn sx={{ color: "white" }} />
                        <Box sx={{ display: { xs: "none", md: "block" } }}>
                          {location?.name || "Select Location"}
                        </Box>
                      </Box>
                    );
                  }}
                  displayEmpty
                  sx={{
                    color: "white",
                    ".MuiOutlinedInput-notchedOutline": {
                      borderColor: "rgba(255, 255, 255, 0.5)",
                    },
                    "&:hover .MuiOutlinedInput-notchedOutline": {
                      borderColor: "rgba(255, 255, 255, 0.7)",
                    },
                    "&.Mui-focused .MuiOutlinedInput-notchedOutline": {
                      borderColor: "white",
                    },
                    ".MuiSvgIcon-root": {
                      color: "white",
                    },
                  }}
                >
                  <MenuItem value="" disabled>
                    Select Location
                  </MenuItem>
                  {Array.isArray(allLocations) &&
                    allLocations.map((location) => (
                      <MenuItem
                        key={location.location_Id}
                        value={location.location_Id}
                      >
                        {location.name}
                      </MenuItem>
                    ))}
                </Select>
              </FormControl>
            )}

          {!isLoginPage && (
            <Button
              color="inherit"
              onClick={
                isAuthenticated
                  ? handleLogout
                  : () => handleNavigation("/auth/login")
              }
              endIcon={isAuthenticated ? <LogOut /> : <LogIn />}
              sx={{
                minWidth: { xs: 56, sm: "auto" },
                px: { xs: 2, sm: 2.5 },
                justifyContent: "flex-end",
                "& .MuiButton-endIcon": {
                  margin: 0,
                  marginLeft: 1,
                },
              }}
            >
              <Typography sx={{ display: { xs: "none", sm: "block" } }}>
                {isAuthenticated || isGuest ? "Logout" : "Login"}
              </Typography>
            </Button>
          )}
        </Box>

        <Drawer
          anchor="left"
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
          sx={{
            "& .MuiDrawer-paper": {
              top: { xs: 56, sm: 64 },
            },
          }}
        >
          {renderMenuList()}
        </Drawer>
      </Toolbar>
    {loading && (
      <Box
        role="status"
        aria-label="Loading"
        sx={{
            position: "absolute",
            top: "calc(100% + 8px)",
          left: "50%",
          transform: "translateX(-50%)",
          zIndex: theme.zIndex.drawer + 2,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          pointerEvents: "none",
        }}
      >
        <CircularProgress size={24} />
      </Box>
    )}
    </AppBar>
  );
};

export default AppBarWithTitle;
