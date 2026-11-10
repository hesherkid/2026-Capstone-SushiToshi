"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Alert,
  Box,
  Button,
  ButtonBase,
  InputAdornment,
  MenuItem,
  TextField,
  Typography,
} from "@mui/material";
import api from "@/config/api";
import useAuth from "@/hooks/useAuth";
import { decodeToken } from "@/config/auth";
import { getAuthToken, isGuestClaims } from "@/utils/auth";
import { hasTableVisit } from "@/utils/tableVisit";
import storage from "@/utils/storage";
import MenuItemCard from "@/components/menu/MenuItemCard";
import MenuBrowser from "@/components/menu/MenuBrowser";
import useMyItems from "@/components/menu/useMyItems";
import {
  groupItemsByCategory,
  normalizeCategory,
} from "@/components/menu/cartRules";
import { RESTAURANT_TIME_ZONE } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import MaterialSymbol from "@/components/common/MaterialSymbol";

const LOGIN_PATH = "/auth/login";
const MENU_PATH = "/menu/full-menu";
// The location picked here last; otherwise the one whose table QR code was scanned last
const BROWSE_LOCATION_KEY = "browseLocationId";

// "14:05:09" in the restaurant's time zone, to compare with a menu's "HH:mm:ss" hours
const timeNow = () =>
  new Date().toLocaleTimeString("en-GB", {
    timeZone: RESTAURANT_TIME_ZONE,
    hour12: false,
  });

// Whether a menu's hours cover the time; hours past midnight (22:00 to 02:00) wrap around
const servedAt = (menu, time) => {
  const start = menu.start_time ?? "";
  const end = menu.end_time ?? "";
  if (start === end) return true;
  return start < end
    ? time >= start && time < end
    : time >= start || time < end;
};

// "11:00:00" as "11 a.m.", "17:30:00" as "5:30 p.m."
const clockText = (value) => {
  const [hours, minutes] = String(value ?? "")
    .split(":")
    .map(Number);
  if (Number.isNaN(hours) || Number.isNaN(minutes)) return "";
  return new Date(2000, 0, 1, hours, minutes).toLocaleTimeString("en-CA", {
    hour: "numeric",
    ...(minutes ? { minute: "2-digit" } : {}),
  });
};

// "11 a.m.–3 p.m."; a menu served all day has no hours
const menuHours = (menu) =>
  menu.start_time === menu.end_time
    ? ""
    : `${clockText(menu.start_time)}–${clockText(menu.end_time)}`;

// "Lunch · 11 a.m.–3 p.m."
const menuLabel = (menu) =>
  [menu.name, menuHours(menu)].filter(Boolean).join(" · ");

// Same pills as the menu's tag filter
const pill = (selected) => ({
  flexShrink: 0,
  height: 32,
  px: 1.5,
  borderRadius: 999,
  fontSize: "0.85rem",
  whiteSpace: "nowrap",
  fontWeight: selected ? 700 : 500,
  bgcolor: selected ? "primary.main" : "grey.100",
  color: selected ? "common.white" : "grey.800",
  transition: "background-color 150ms ease, color 150ms ease",
  "&:hover": { bgcolor: selected ? "primary.dark" : "grey.200" },
  "&.Mui-focusVisible": {
    outline: "2px solid",
    outlineColor: "primary.main",
    outlineOffset: 2,
  },
});

const firstLocation = (locations) => {
  const ids = locations.map((location) => location.location_id);
  return (
    [storage.get(BROWSE_LOCATION_KEY), storage.get("locationId")]
      .map(Number)
      .find((id) => ids.includes(id)) ??
    ids[0] ??
    ""
  );
};

/**
 * The menu for customers with an account who aren't seated yet: what each location serves and
 * at what times, with favourites. Looking only; ordering starts by scanning the table's QR code.
 * Guests and seated customers go to their table's menu, signed-out visitors to sign in.
 */
const BrowseMenuPage = () => {
  const router = useRouter();
  const { isAuthenticated, loading: authLoading } = useAuth();
  const [ready, setReady] = useState(false);
  const [error, setError] = useState(null);

  const [locations, setLocations] = useState([]);
  const [locationId, setLocationId] = useState("");
  // The location's active menus; null while they load
  const [menus, setMenus] = useState(null);
  const [menuId, setMenuId] = useState("");
  const [categories, setCategories] = useState([]);
  const [menuItems, setMenuItems] = useState({});
  const [itemsLoaded, setItemsLoaded] = useState(false);

  const myItems = useMyItems({ onError: setError });
  const { canSave, favourites, toggleFavourite } = myItems;

  useEffect(() => {
    if (authLoading) return;
    const token = getAuthToken();
    if (hasTableVisit() || (token && isGuestClaims(decodeToken(token)))) {
      router.replace(MENU_PATH);
      return;
    }
    if (!isAuthenticated) {
      router.replace(LOGIN_PATH);
      return;
    }
    setReady(true);
  }, [authLoading, isAuthenticated, router]);

  // Locations and categories once; the menus follow the location picked
  useEffect(() => {
    if (!ready) return;
    api
      .get("/Location")
      .then(({ data }) => {
        const list = Array.isArray(data) ? data : [];
        setLocations(list);
        setLocationId(firstLocation(list));
        if (list.length === 0) setError("There's no menu to show yet.");
      })
      .catch((err) => {
        console.error("Error fetching locations:", err);
        setError("Couldn't load the menu. Please try again.");
      });
    api
      .get("/Category")
      .then(({ data }) =>
        setCategories((Array.isArray(data) ? data : []).map(normalizeCategory)),
      )
      .catch((err) => console.error("Error fetching categories:", err));
  }, [ready]);

  // The location's menus, starting on the one served now
  useEffect(() => {
    if (!locationId) return;
    let cancelled = false;
    setMenus(null);
    setMenuId("");
    setItemsLoaded(false);
    api
      .get(`/Menu/location/${locationId}`)
      .then(({ data }) => {
        if (cancelled) return;
        const list = (Array.isArray(data) ? data : []).sort((a, b) =>
          String(a.start_time).localeCompare(String(b.start_time)),
        );
        setMenus(list);
        const now = timeNow();
        setMenuId(
          (list.find((menu) => servedAt(menu, now)) ?? list[0])?.menu_id ?? "",
        );
      })
      .catch((err) => {
        if (cancelled) return;
        console.error(`Error fetching menus for location ${locationId}:`, err);
        setMenus([]);
        setError("Couldn't load the menu. Please try again.");
      });
    return () => {
      cancelled = true;
    };
  }, [locationId]);

  // The menu's items, priced for the location
  useEffect(() => {
    if (!menuId) return;
    let cancelled = false;
    setItemsLoaded(false);
    api
      .get(`/Menu/${menuId}/item`, { params: { location_id: locationId } })
      .then(({ data }) => {
        if (!cancelled)
          setMenuItems(groupItemsByCategory(Array.isArray(data) ? data : []));
      })
      .catch((err) => {
        if (cancelled) return;
        console.error(`Error fetching menu items for menu ${menuId}:`, err);
        setMenuItems({});
        setError("Couldn't load the menu. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setItemsLoaded(true);
      });
    return () => {
      cancelled = true;
    };
  }, [menuId, locationId]);

  const chooseLocation = (id) => {
    setError(null);
    setLocationId(id);
    storage.set(BROWSE_LOCATION_KEY, String(id));
  };

  if (!ready) return null;

  const noMenus = menus?.length === 0 && !error;
  const locationName =
    locations.find((location) => location.location_id === locationId)?.name ??
    "";

  return (
    <CustomerPageContainer>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 1,
          mt: { xs: 3, sm: 2 },
        }}
      >
        <Button
          onClick={() => router.push("/")}
          aria-label="Back to home"
          sx={{ minWidth: 0, p: 1, borderRadius: 999, color: "text.primary" }}
        >
          <MaterialSymbol name="arrow_back" size={24} />
        </Button>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700 }}>
          Our menu
        </Typography>
      </Box>

      {/* The location is only a choice when there's more than one */}
      {locations.length > 1 ? (
        <TextField
          select
          fullWidth
          size="small"
          label="Location"
          value={locationId}
          onChange={(event) => chooseLocation(event.target.value)}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <MaterialSymbol
                    name="location_on"
                    filled
                    size={20}
                    sx={{ color: "primary.main" }}
                  />
                </InputAdornment>
              ),
            },
            select: {
              // Just the name in the field; the list's check marks stay in the list
              renderValue: () => locationName,
              MenuProps: {
                slotProps: {
                  paper: {
                    variant: "outlined",
                    elevation: 0,
                    sx: {
                      mt: 0.5,
                      borderRadius: 2,
                      boxShadow: "0 4px 12px rgba(0, 0, 0, 0.08)",
                    },
                  },
                  list: { dense: true, sx: { py: 0.5 } },
                },
              },
            },
          }}
          sx={{
            mt: 1,
            mb: 2,
            textAlign: "left",
            "& .MuiOutlinedInput-root": { borderRadius: 2 },
          }}
        >
          {locations.map((location) => (
            <MenuItem
              key={location.location_id}
              value={location.location_id}
              sx={{
                gap: 1,
                py: 1,
                fontSize: "0.95rem",
                justifyContent: "space-between",
                // A check marks the current one, rather than a highlight
                "&.Mui-selected, &.Mui-selected:hover": {
                  bgcolor: "transparent",
                },
                "&.Mui-selected:hover, &:hover": { bgcolor: "grey.100" },
              }}
            >
              {location.name}
              {location.location_id === locationId && (
                <MaterialSymbol
                  name="check"
                  size={18}
                  sx={{ color: "primary.main" }}
                />
              )}
            </MenuItem>
          ))}
        </TextField>
      ) : (
        locationName && (
          // Lined up with the title, past the back arrow
          <Typography
            sx={{
              display: "flex",
              alignItems: "center",
              gap: 0.5,
              pl: 6,
              mb: 2.5,
              fontWeight: 600,
              fontSize: "0.95rem",
            }}
          >
            <MaterialSymbol
              name="location_on"
              filled
              size={18}
              sx={{ color: "primary.main" }}
            />
            {locationName}
          </Typography>
        )
      )}

      {/* One menu needs no picking */}
      {menus?.length > 1 && (
        <Box
          role="group"
          aria-label="Menus"
          sx={{
            display: "flex",
            gap: 1,
            mb: 2.5,
            overflowX: "auto",
            scrollbarWidth: "none",
            "&::-webkit-scrollbar": { display: "none" },
          }}
        >
          {menus.map((menu) => (
            <ButtonBase
              key={menu.menu_id}
              aria-pressed={menu.menu_id === menuId}
              onClick={() => setMenuId(menu.menu_id)}
              sx={pill(menu.menu_id === menuId)}
            >
              {menuLabel(menu)}
            </ButtonBase>
          ))}
        </Box>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {noMenus ? (
        <Alert severity="info">
          This location has no menu to show right now.
        </Alert>
      ) : (
        <MenuBrowser
          categories={categories}
          menuItems={menuItems}
          loading={!itemsLoaded && !error}
          ready={Boolean(menuId && itemsLoaded)}
          myItems={myItems}
          renderItem={(item, category) => (
            <MenuItemCard
              key={`item-${category.category_id}-${item.item_id}`}
              item={item}
              categoryId={category.category_id}
              quantity={0}
              // Ordering happens at the table
              showQuantityControls={false}
              favourite={favourites.has(item.item_id)}
              onToggleFavourite={canSave ? toggleFavourite : undefined}
            />
          )}
        />
      )}
    </CustomerPageContainer>
  );
};

export default BrowseMenuPage;
