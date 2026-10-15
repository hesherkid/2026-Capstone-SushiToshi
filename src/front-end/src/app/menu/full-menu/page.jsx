"use client";
import { useEffect, useState } from "react";
import {
  Typography,
  Box,
  Button,
  Container,
  Alert,
  TextField,
  Divider,
} from "@mui/material";
import { Search } from "lucide-react";
import MenuItemCard from "@/components/menu/MenuItemCard";
import CartDrawer from "@/components/menu/CartDrawer";
import TrackedItem from "@/components/menu/TrackedItem";
import useMenuSearch from "@/components/menu/searchUtils";
import api from "@/config/api";
import publicApi from "@/config/publicApi";
import { useOrderActions } from "@/hooks/useOrderActions";

//Check if the logged in user is a guest.
const isGuest = () => typeof window !== "undefined" && localStorage.getItem("guest") === "true";

const FullMenu = () => {
  const [categories, setCategories] = useState([]);
  const [menuItems, setMenuItems] = useState({});
  const [quantities, setQuantities] = useState({});
  const [sessionId, setSessionId] = useState(null);
  const [error, setError] = useState(null);
  const [selectedBillId, setSelectedBillId] = useState("");
  const [menuId, setMenuId] = useState("");
  const [locationId, setLocationId] = useState(null);
  const [orderSuccess, setOrderSuccess] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState("all");
  const [cartDrawerOpen, setCartDrawerOpen] = useState(false);

  const handleOrderSuccess = () => {
    setOrderSuccess(true);
    setQuantities({});
    
    // Redirect to orders page
    setTimeout(() => {
      window.location.href = "/dashboard/orders";
    }, 2000);

    // Hide success message after 3 seconds
    setTimeout(() => {
      setOrderSuccess(false);
    }, 3000);
  };

  const {
    createOrder,
    addOrderItems,
    actionError,
    isLoading,
    clearActionError,
  } = useOrderActions(handleOrderSuccess);

  const handleQuantityChange = (categoryId, itemId, delta) => {
    setQuantities((prev) => ({
      ...prev,
      [categoryId]: {
        ...prev[categoryId],
        [itemId]: Math.max(0, (prev[categoryId]?.[itemId] || 0) + delta),
      },
    }));
  };

  const handleSearchChange = (event) => {
    setSearchTerm(event.target.value);
  };

  const {
    searchTerm,
    setSearchTerm,
    searchResults: fuzzySearchResults,
  } = useMenuSearch(Object.values(menuItems).flat().filter(Boolean));

  const filterMenuItems = (items, categoryId) => {
    if (!items) return [];

    return items.filter((item) => {
      if (quantities[categoryId]?.[item.item_id] > 0) return true;

      if (!searchTerm.trim()) return true;

      const searchTerms = searchTerm
        .toLowerCase()
        .split(" ")
        .map((term) => term.trim())
        .filter((term) => term.length > 0);

      if (searchTerms.length === 0) return true;

      return searchTerms.some((term) => {
        return (
          item.name.toLowerCase().includes(term) ||
          (item.description && item.description.toLowerCase().includes(term)) ||
          (item.tags &&
            item.tags.some((tag) => tag.name.toLowerCase().includes(term)))
        );
      });
    });
  };

  const getSelectedItemsStats = (categoryId) => {
    if (!quantities[categoryId]) return { itemCount: 0, totalQuantity: 0 };

    const items = Object.entries(quantities[categoryId]);
    return items.reduce(
      (stats, [_, quantity]) => ({
        itemCount: quantity > 0 ? stats.itemCount + 1 : stats.itemCount,
        totalQuantity: stats.totalQuantity + quantity,
      }),
      { itemCount: 0, totalQuantity: 0 }
    );
  };

  const getTotalStats = () => {
    return Object.keys(quantities).reduce(
      (totals, categoryId) => {
        const categoryStats = getSelectedItemsStats(categoryId);
        return {
          itemCount: totals.itemCount + categoryStats.itemCount,
          totalQuantity: totals.totalQuantity + categoryStats.totalQuantity,
        };
      },
      { itemCount: 0, totalQuantity: 0 }
    );
  };

  useEffect(() => {
    const count = Object.values(quantities).reduce(
      (total, categoryQuantities) =>
        total + Object.values(categoryQuantities).reduce((categoryTotal, quantity) => categoryTotal + quantity, 0),
      0
    );

    window.dispatchEvent(
      new CustomEvent("menu-cart-count-changed", { detail: { count } })
    );
  }, [quantities]);

  useEffect(() => {
    const openCart = () => setCartDrawerOpen(true);
    window.addEventListener("menu-cart-open", openCart);
    return () => window.removeEventListener("menu-cart-open", openCart);
  }, []);

  useEffect(() => {
    const getActiveSession = async () => {
      // Handle guest users separately
      if (isGuest()) {
        const sid = localStorage.getItem("session_id");
        if (sid) {
          setSessionId(parseInt(sid));
        } else {
          setError("Guest session not found. Please scan your table QR again.");
        }
        return;
      }

      // For authenticated users only
      try {
        setError(null);
        const response = await api.get(
          "/DiningSession/participants/active-session-id"
        );
        if (response?.status >= 200 && response.status < 300 && response.data) {
          setSessionId(response.data.session_id ?? response.data);
        } else {
          setError("No active session found");
        }
      } catch (err) {
        console.error("Error fetching session:", err);
        setError(err.response?.data?.detail || "Error fetching session");
      }
    };

    getActiveSession();
  }, []);

  useEffect(() => {
    if (!sessionId || sessionId < 1) return;

    const selectCurrentBill = async () => {
      try {
        const apiClient = isGuest() ? publicApi : api;
        const response = await apiClient.get(`/Bill/get_bills/${sessionId}`);
        const bills = Array.isArray(response.data) ? response.data : [];

        if (!bills.length) {
          setSelectedBillId("");
          return;
        }

        const sortedBills = [...bills].sort(
          (a, b) =>
            new Date(
              b.created_At ?? b.created_at ?? b.Created_At ?? 0
            ).getTime() -
            new Date(
              a.created_At ?? a.created_at ?? a.Created_At ?? 0
            ).getTime()
        );

        const currentBill =
          sortedBills.find(
            (bill) =>
              (bill.status ?? bill.Status ?? "")
                .toString()
                .toUpperCase() !== "CLOSED"
          ) ?? sortedBills[0];

        const billId = Number(currentBill?.bill_id ?? currentBill?.Bill_Id);

        if (!Number.isNaN(billId) && billId > 0) {
          setSelectedBillId(billId);
          localStorage.setItem("currentBillId", String(billId));
        }
      } catch (err) {
        console.error("Error fetching current bill:", err);
      }
    };

    selectCurrentBill();
  }, [sessionId]);

  useEffect(() => {
    //Session ID can only be 1 or above
    if (!sessionId || sessionId < 1) {

      return;
    }

    const fetchMenu = async () => {

      const apiClient = isGuest() ? publicApi : api;
      try {
        setError(null);

        const response = await apiClient.get(`/DiningSession/session-menu/${sessionId}`);

        if (response?.status >= 200 && response.status < 300 && response.data) {
          const raw = response.data;

          const id = raw.menu_id ?? raw.menuId ?? raw.Menu_Id ?? raw.menu_Id ?? raw.MenuID ?? raw;
          const getMenuID =
            typeof id === "number" ? id : parseInt(String(id), 10);

          if (Number.isNaN(getMenuID) || getMenuID < 1) {
            console.warn("Menu ID is invalid:", raw);
            setError("No menu for this session");
            return;
          }

          // Location decides which prices this table sees
          const loc = Number(raw.location_Id ?? raw.location_id ?? raw.locationId ?? raw.Location_Id);
          setLocationId(Number.isNaN(loc) || loc < 1 ? null : loc);
          setMenuId(getMenuID);

        } else {
          console.warn("No menu found", response?.data);
          setError("No menu found for this session");
        }
      } catch (err) {
        console.error("Error fetching menu:", err);
        const message =
          err?.response?.data?.detail ??
          err?.response?.data?.message ??
          err?.message ??
          "Error fetching menu";
        setError(message);
      }
    };

    fetchMenu();
  }, [sessionId]);

  //When you have the menuID call to get current menu (prices depend on location)
  useEffect(() => {
    if (!menuId) return;
    fetchMenuItems(null, menuId, locationId);
  }, [menuId, locationId]);


  useEffect(() => {
    const fetchCategories = async () => {

      const apiClient = isGuest() ? publicApi : api;

      try {
        setError(null);
        const response = await apiClient.get("/Category");
        const data = Array.isArray(response.data) ? response.data : [];
        setCategories(
          data.map((c) => ({
            category_id: c.category_id ?? c.Category_id,
            name: c.name ?? c.category_name ?? c.Category_name,
          }))
        );
      } catch (err) {
        console.error("Error fetching categories:", err);
        const message =
          err?.response?.data?.detail ??
          err?.response?.data?.message ??
          err?.message ??
          "Error fetching categories";
        setError(message);
      }
    };
    fetchCategories();
  }, []);

  const fetchMenuItems = async (categoryId, menu_id, location_id = null) => {
    //Make sure the menu ID is valid or if we already loaded the menu items
    if (!menu_id || menuItems[categoryId]) return;

    const apiClient = isGuest() ? publicApi : api;

    try {
      //Call GetItemsByMenu with the session's location so location prices apply
      const response = await apiClient.get(`/Menu/${menu_id}/item`, {
        params: location_id ? { locationId: location_id } : {},
      });
      const items = Array.isArray(response.data) ? response.data : [];

      //Clean up the field names so it's the same everywhere on the page
      const normalized = items.map((it) => ({
        item_id: it.item_id ?? it.itemId ?? it.Item_Id,
        name: it.name,
        description: it.description,
        price: it.price,
        is_add_on: it.is_add_on ?? it.isAddOn,
        item_image_url: it.item_image_url ?? it.imageURL ?? it.imageUrl,
        category_id: it.categoryID ?? it.categoryId ?? it.category_id,
        category: it.category,
        tags: it.tags ?? [],
      }));

      //Group the items so all each category has its own list
      const grouped = normalized.reduce((acc, item) => {
        (acc[item.category_id] ||= []).push(item);
        return acc;
      }, {});

      setMenuItems((prev) => ({ ...prev, ...grouped }));
    } catch (err) {
      console.error(`Error fetching menu items for menu ${menu_id}:`, err);
    }
  };

  const handleBillChange = (event) => {
    const billId = Number(event.target.value);
    setSelectedBillId(billId);
  };

  // Creates an array of order items from the amounts selected
  const createOrderItems = () => {
    const orderItems = Object.entries(quantities).flatMap(
      ([categoryId, items]) =>
        Object.entries(items)
          .filter(([_, quantity]) => quantity > 0)
          .map(([itemId, quantity]) => {
            const menuItem = menuItems[categoryId]?.find(
              (item) => item.item_id === parseInt(itemId)
            );

            if (!menuItem) {
              throw new Error(`Menu item ${itemId} not found`);
            }

            let price_at_time = menuItem.is_add_on
              ? Number(menuItem.price) || 0
              : 0;

            return {
              menu_id: Number(menuId),
              item_id: Number(itemId),
              quantity: Number(quantity),
              price_at_time,
            };
          })
    );

    return orderItems;
  };

  const handleConfirmOrder = async () => {
    if (!selectedBillId || !sessionId || !menuId) {
      setError("Please select a bill first");
      return;
    }

    const hasItems = Object.values(quantities).some((category) =>
      Object.values(category).some((quantity) => quantity > 0)
    );
    if (!hasItems) {
      setError("Please select at least one item");
      return;
    }

    setError(null);
    clearActionError();

    try {
      const orderResponse = await createOrder(sessionId, selectedBillId);

      if (!orderResponse) throw new Error("Failed to create order");

      const newOrderId = Number(orderResponse.order_Id);

      const orderItems = createOrderItems();

      await addOrderItems(newOrderId, orderItems);
    } catch (error) {
      console.error("Error processing order:", error);
      setError(error.message || "Error processing order");
    }
  };

  // Combine local and hook errors
  const displayError = error || actionError;

  const filteredMenuGroups = categories
    .filter(
      (category) =>
        selectedCategory === "all" || category.category_id === selectedCategory
    )
    .map((category) => ({
      category,
      items: filterMenuItems(menuItems[category.category_id], category.category_id),
    }))
    .filter(({ items }) => items.length > 0);

  const allMenuItemCount = categories.reduce(
    (total, category) =>
      total +
      filterMenuItems(menuItems[category.category_id], category.category_id).length,
    0
  );

  return (

    <Container disableGutters maxWidth={false}>
      <Box sx={{ p: 0 }}>
        <Typography
          variant="h5"
          component="h1"
          sx={{ mt: 4, mb: 4, textAlign: "left", width: "100%", fontWeight: 200,}}
        >
          All You Can Eat Menu
        </Typography>

        {/* Error Alert */}
        {displayError && (
          <Alert
            severity="error"
            sx={{ mb: 2 }}
            onClose={() => {
              setError(null);
              clearActionError();
            }}
          >
            {displayError}
          </Alert>
        )}

        <Box sx={{ width: "100%", mb: 3 }}>
          {sessionId ? (
            <>
              <TextField
                fullWidth
                placeholder="Search the menu..."
                value={searchTerm}
                onChange={handleSearchChange}
                InputProps={{
                  startAdornment: (
                    <Search size={20} style={{ marginRight: 8 }} />
                  ),
                  endAdornment: searchTerm ? (
                    <Button
                      onClick={() => setSearchTerm("")}
                      sx={{
                        minWidth: "auto",
                        p: 0.5,
                        color: "text.secondary",
                        "&:hover": {
                          backgroundColor: "transparent",
                          color: "text.primary",
                        },
                      }}
                    >
                      ✕
                    </Button>
                  ) : null,
                }}
                sx={{ mb: 2 }}
              />

              {categories.length > 0 && (
                <Box
                  sx={{
                    display: "flex",
                    gap: 1,
                    overflowX: "auto",
                    pb: 1,
                    mb: 2,
                    "&::-webkit-scrollbar": { display: "none" },
                  }}
                >
                  <Button
                    variant={selectedCategory === "all" ? "contained" : "outlined"}
                    onClick={() => setSelectedCategory("all")}
                    sx={{
                      minWidth: "max-content",
                      bgcolor: selectedCategory === "all" ? "#C01E2E" : "transparent",
                      color: selectedCategory === "all" ? "#fff" : "#C01E2E",
                      borderColor: "#C01E2E",
                    }}
                  >
                    Full Menu
                  </Button>

                  {categories.map((category) => (
                    <Button
                      key={`cat-btn-${category.category_id}`}
                      variant={
                        selectedCategory === category.category_id
                          ? "contained"
                          : "outlined"
                      }
                      onClick={() => setSelectedCategory(category.category_id)}
                      sx={{
                        minWidth: "max-content",
                        bgcolor:
                          selectedCategory === category.category_id
                            ? "#C01E2E"
                            : "transparent",
                        color:
                          selectedCategory === category.category_id
                            ? "#fff"
                            : "#C01E2E",
                        borderColor: "#C01E2E",
                      }}
                    >
                      {category.name || `Category ${category.category_id}`}
                    </Button>
                    
                  ))}
                </Box>
                
              )}
            </>
          ) : (
            <Alert severity="info">No active session found</Alert>
          )}
          
        </Box>

        {sessionId && menuId && (
          <Box sx={{ width: "100%", mb: 4 }}>
            <Divider sx={{ mb: 2 }} />

            {!selectedBillId && (
              <Alert severity="warning" sx={{ mb: 2 }}>
                Your menu is available, but there is no open bill for this session yet.
                Ask a staff member to open a bill before placing an order.
              </Alert>
            )}

            {selectedCategory === "all" && (
              <Typography
                variant="h6"
                component="h2"
                sx={{
                  fontWeight: 600,
                  mb: 2,
                  color: "#1f2937",
                  textAlign: "left",
                }}
              >
                Viewing Full Menu{" "}
                <Typography
                  component="span"
                  sx={{
                    fontSize: "0.75rem",
                    fontWeight: 400,
                    color: "#6b7280",
                    ml: 0.5,
                  }}
                >
                  {allMenuItemCount} items
                </Typography>
              </Typography>
  
            )}

            {filteredMenuGroups.length === 0 ? (
              <Alert severity="info">No items match your search.</Alert>
            ) : (
              filteredMenuGroups.map(({ category, items }) => (
                <Box key={`group-${category.category_id}`} sx={{ mb: 3 }}>
                  <Typography
                    variant="h5"
                    component="h2"
                    sx={{
                      fontWeight: 600,
                      mb: 1.5,
                      color: "#1f2937",
                      textAlign: "left",
                    }}
                  >
                    {category.name || `Category ${category.category_id}`}{" "}
                    <Typography
                      component="span"
                      sx={{
                        fontSize: "0.75rem",
                        fontWeight: 400,
                        color: "#6b7280",
                        ml: 0.5,
                      }}
                    >
                      {items.length} items
                    </Typography>
                  </Typography>

                  <Box
                    sx={{
                      display: "flex",
                      flexDirection: "column",
                      gap: 1.5,
                    }}
                  >
                    {items.map((item) => (
                      // Records how long the card is on screen (browsing behavior analytics)
                      <TrackedItem
                        key={`item-${category.category_id}-${item.item_id}`}
                        itemId={item.item_id}
                        sessionId={sessionId}
                      >
                        <MenuItemCard
                          item={item}
                          categoryId={category.category_id}
                          quantity={
                            quantities[category.category_id]?.[item.item_id] || 0
                          }
                          onQuantityChange={handleQuantityChange}
                        />
                      </TrackedItem>
                    ))}
                  </Box>
                </Box>
              ))
            )}

            {orderSuccess && (
              <Alert severity="success" sx={{ mb: 2 }}>
                Order placed successfully!
              </Alert>
            )}

          </Box>
        )}

        <CartDrawer
          open={cartDrawerOpen}
          onClose={() => setCartDrawerOpen(false)}
          categories={categories}
          quantities={quantities}
          menuItems={menuItems}
          onQuantityChange={handleQuantityChange}
          onConfirmOrder={handleConfirmOrder}
          isLoading={isLoading}
          selectedBillId={selectedBillId}
          itemCount={getTotalStats().itemCount}
          totalQuantity={getTotalStats().totalQuantity}
        />
      </Box>
    </Container>
  );
};

export default FullMenu;