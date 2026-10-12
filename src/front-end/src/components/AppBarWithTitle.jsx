"use client";
import { useState, useEffect } from "react";
import {
  AppBar,
  Toolbar,
  Typography,
  Button,
  IconButton,
  Box,
  useTheme,
  CircularProgress,
  Badge,
  Select,
  MenuItem,
  FormControl,
} from "@mui/material";
import {
  Menu as MenuIcon,
  LogIn,
  ShoppingBag,
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
import AppNavigationDrawer from "@/components/AppNavigationDrawer";

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
  const [tableNumber, setTableNumber] = useState("");
  const [bagItemCount, setBagItemCount] = useState(0);
  const { isAuthenticated, userRole, loading } = useAuth();
  const { notifySuccess, notifyError, notifyInfo } = useNotification(); 
  const isGuest = typeof window !== "undefined" && localStorage.getItem("guest") === "true";
  const canShowDrawer =
    !isLoginPage && (isAuthenticated || isGuest);

  useEffect(() => {
    if (typeof window === "undefined") return;

    const handleCartCountChange = (event) => {
      setBagItemCount(Number(event.detail?.count) || 0);
    };

    window.addEventListener("menu-cart-count-changed", handleCartCountChange);
    return () => {
      window.removeEventListener("menu-cart-count-changed", handleCartCountChange);
    };
  }, []);

  // The app bar stays mounted across client-side navigation, so re-read the
  // table number on each route change (e.g. after the QR login saves it)
  useEffect(() => {
    setTableNumber(localStorage.getItem("tableNumber") || "");
  }, [pathname]);

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
  }, [isAuthenticated, userRole, notifyError, notifyInfo]);

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
  
const handleLogout = () => {
  if (typeof window !== "undefined") {
    ["guest", "session_id", "guest_oid", "tableNumber", "locationId", "currentBillId"].forEach(
      (key) => localStorage.removeItem(key)
    );
  }

  setDrawerOpen(false);
  logoutUser();
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
  const effectiveRole = userRole || (isGuest ? "customer" : null);

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
            justifyContent: "flex-start",
            gap: { xs: 2, sm: 3},
            flex: "0 0 auto",
            minWidth: 0,
          }}
        >
          {canShowDrawer && (
            <IconButton
              color="inherit"
              aria-label="menu"
              onClick={() => setDrawerOpen(true)}
              sx={{ p: { xs: 1, sm: 1.5 }, flexShrink: 0,}}
            >
              <MenuIcon />
            </IconButton>
          )}
          {/* <Typography
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
          </Typography> */}
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
            gap: { xs: 0.5, sm: 2 },
          }}
        >
          {(isGuest || userRole === "customer") && tableNumber && (
            <Box
              aria-label={`Table ${tableNumber}`}
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 0.75,
                px: { xs: 1, sm: 1.5 },
                py: 0.5,
                borderRadius: 2,
                border: "1px solid rgba(255, 255, 255, 0.3)",
                backgroundColor: "rgba(255, 255, 255, 0.12)",
                color: "inherit",
                whiteSpace: "nowrap",
                flexShrink: 0,
              }}
            >
              <Typography
                component="span"
                sx={{
                  fontSize: { xs: "0.9rem", sm: "0.875rem" },
                  fontWeight: 600,
                  letterSpacing: "0.02em",
                  lineHeight: 1.4,
                }}
              >
                Table {tableNumber}
              </Typography>
            </Box>
          )}

          {(isGuest || userRole === "customer") && pathname === "/menu/full-menu" && (
            <IconButton
              color="inherit"
              aria-label={`Shopping bag, ${bagItemCount} ${bagItemCount === 1 ? "item" : "items"}`}
              disabled={bagItemCount === 0}
              onClick={() =>
                window.dispatchEvent(new Event("menu-cart-open"))
              }
              sx={{
                color: "white",
                "&.Mui-disabled": { color: "white" },
              }}
            >
              <Badge
                badgeContent={bagItemCount}
                showZero
                sx={{
                  "& .MuiBadge-badge": {
                    backgroundColor: "white",
                    color: theme.palette.primary.main,
                    border: `1.5px solid ${theme.palette.primary.main}`,
                    fontWeight: 700,
                  },
                }}
              >
                <ShoppingBag size={25} />
              </Badge>
            </IconButton>
          )}

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

          {!isLoginPage && !isAuthenticated && !isGuest && (
            <Button
              color="inherit"
              onClick={() => handleNavigation("/auth/login")}
              endIcon={<LogIn />}
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
                Login
              </Typography>
            </Button>
          )}
        </Box>

        {canShowDrawer && (
          <AppNavigationDrawer
            open={drawerOpen}
            role={effectiveRole}
            onClose={() => setDrawerOpen(false)}
            onNavigate={handleNavigation}
            onLogout={handleLogout}
          />
        )}
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
