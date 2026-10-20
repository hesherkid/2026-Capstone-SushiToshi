"use client";
import { useEffect, useRef, useState } from "react";
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
import { MenuListSkeleton } from "@/components/menu/MenuItemCardSkeleton";
import TrackedItem from "@/components/menu/TrackedItem";
import useMenuSearch from "@/components/menu/searchUtils";
import api from "@/config/api";
import { fetchMySessionId, NOT_SEATED_MESSAGE } from "@/services/tableSession";

// localStorage key for the items picked but not yet ordered
const CART_STORAGE_KEY = "menuCart";

const BILL_REQUESTED_MESSAGE =
  "You've asked for your bill, so no more orders can be added.";
import { useOrderActions } from "@/hooks/useOrderActions";

//Check if the logged in user is a guest.
const isGuest = () =>
  typeof window !== "undefined" && localStorage.getItem("guest") === "true";

const FullMenu = () => {
  const [categories, setCategories] = useState([]);
  const [menuItems, setMenuItems] = useState({});
  // False until the menu's items have been fetched (or the fetch failed)
  const [itemsLoaded, setItemsLoaded] = useState(false);
  const [quantities, setQuantities] = useState({});
  const [sessionId, setSessionId] = useState(null);
  // False until the server says which table (if any) this person is at
  const [sessionChecked, setSessionChecked] = useState(false);
  const [error, setError] = useState(null);
  const [selectedBillId, setSelectedBillId] = useState("");
  // The table's open bills, newest first; the orderer picks which one this order goes on
  const [openBills, setOpenBills] = useState([]);
  // The guests pressed "Ready for Bill" on this bill, so the server won't take more orders on it
  const billRequested = Boolean(
    openBills.find((bill) => bill.bill_id === selectedBillId)?.requested_at,
  );
  const [menuId, setMenuId] = useState("");
  const [locationId, setLocationId] = useState(null);
  const [orderSuccess, setOrderSuccess] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState("all");
  const [cartDrawerOpen, setCartDrawerOpen] = useState(false);
  // Order created but its items not added yet (e.g. a limit was hit); reused on the next try
  const PENDING_ORDER_KEY = "pendingOrder";
  const [pendingOrderId, setPendingOrderIdState] = useState(null);

  const setPendingOrderId = (orderId) => {
    setPendingOrderIdState(orderId);
    if (orderId) {
      localStorage.setItem(
        PENDING_ORDER_KEY,
        JSON.stringify({ orderId, sessionId, billId: selectedBillId }),
      );
    } else {
      localStorage.removeItem(PENDING_ORDER_KEY);
    }
  };

  // Restore it once the session and bill are known
  useEffect(() => {
    if (!sessionId || !selectedBillId) return;
    try {
      const saved = JSON.parse(
        localStorage.getItem(PENDING_ORDER_KEY) || "null",
      );
      if (
        saved?.orderId &&
        String(saved.sessionId) === String(sessionId) &&
        String(saved.billId) === String(selectedBillId)
      ) {
        setPendingOrderIdState(Number(saved.orderId));
      } else {
        localStorage.removeItem(PENDING_ORDER_KEY);
        setPendingOrderIdState(null);
      }
    } catch {
      localStorage.removeItem(PENDING_ORDER_KEY);
      setPendingOrderIdState(null);
    }
  }, [sessionId, selectedBillId]);

  const handleOrderSuccess = () => {
    setOrderSuccess(true);
    setQuantities({});
    setPendingOrderId(null);

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
    clearActionError();
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
      { itemCount: 0, totalQuantity: 0 },
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
      { itemCount: 0, totalQuantity: 0 },
    );
  };

  useEffect(() => {
    const count = Object.values(quantities).reduce(
      (total, categoryQuantities) =>
        total +
        Object.values(categoryQuantities).reduce(
          (categoryTotal, quantity) => categoryTotal + quantity,
          0,
        ),
      0,
    );

    window.dispatchEvent(
      new CustomEvent("menu-cart-count-changed", { detail: { count } }),
    );
  }, [quantities]);

  // The cart survives a page refresh. It's saved with its session so a cart from an
  // earlier visit is never restored onto a different table.
  const cartRestored = useRef(false);

  useEffect(() => {
    if (!sessionId || cartRestored.current) return;
    cartRestored.current = true;
    try {
      const saved = JSON.parse(
        localStorage.getItem(CART_STORAGE_KEY) || "null",
      );
      if (saved?.sessionId === sessionId && saved.quantities)
        setQuantities(saved.quantities);
    } catch {
      localStorage.removeItem(CART_STORAGE_KEY);
    }
  }, [sessionId]);

  useEffect(() => {
    // Wait until the saved cart has been read, or the empty starting cart would replace it
    if (!sessionId || !cartRestored.current) return;
    localStorage.setItem(
      CART_STORAGE_KEY,
      JSON.stringify({ sessionId, quantities }),
    );
  }, [sessionId, quantities]);

  useEffect(() => {
    const openCart = () => setCartDrawerOpen(true);
    window.addEventListener("menu-cart-open", openCart);
    return () => window.removeEventListener("menu-cart-open", openCart);
  }, []);

  useEffect(() => {
    const getActiveSession = async () => {
      // Ask the server (guests included) rather than trusting a saved session_id
      try {
        setError(null);
        const mySessionId = await fetchMySessionId();
        if (mySessionId) {
          setSessionId(mySessionId);
        } else {
          setError(NOT_SEATED_MESSAGE);
        }
      } catch (err) {
        console.error("Error fetching session:", err);
        setError(err.response?.data?.detail || "Error fetching session");
      } finally {
        setSessionChecked(true);
      }
    };

    getActiveSession();
  }, []);

  const chooseBill = (billId) => {
    setSelectedBillId(billId);
    localStorage.setItem("currentBillId", String(billId));
  };

  useEffect(() => {
    if (!sessionId || sessionId < 1) return;

    const selectCurrentBill = async () => {
      try {
        const apiClient = api;
        const response = await apiClient.get(`/Bill/get_bills/${sessionId}`);
        const bills = Array.isArray(response.data) ? response.data : [];

        // Only open bills can take orders; newest first
        const open = bills
          .filter((bill) => String(bill.status ?? "").toUpperCase() === "OPEN")
          .map((bill) => ({
            bill_id: Number(bill.bill_id),
            bill_name: bill.bill_name,
            requested_at: bill.requested_at,
            created_at: bill.created_at,
          }))
          .sort((a, b) => new Date(b.created_at) - new Date(a.created_at));
        setOpenBills(open);

        // Keep the bill this phone ordered on last, so a guest doesn't drift onto another
        // party's bill; otherwise the newest bill that can still take orders
        const lastUsed = Number(localStorage.getItem("currentBillId"));
        const current =
          open.find((bill) => bill.bill_id === lastUsed) ??
          open.find((bill) => !bill.requested_at) ??
          open[0];

        if (current) {
          chooseBill(current.bill_id);
        } else {
          setSelectedBillId("");
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
      const apiClient = api;
      try {
        setError(null);

        const response = await apiClient.get(
          `/DiningSession/session-menu/${sessionId}`,
        );

        if (response?.status >= 200 && response.status < 300 && response.data) {
          const raw = response.data;

          const id =
            raw.menu_id ??
            raw.menuId ??
            raw.Menu_Id ??
            raw.menu_Id ??
            raw.MenuID ??
            raw;
          const getMenuID =
            typeof id === "number" ? id : parseInt(String(id), 10);

          if (Number.isNaN(getMenuID) || getMenuID < 1) {
            console.warn("Menu ID is invalid:", raw);
            setError("No menu for this session");
            return;
          }

          // Location decides which prices this table sees
          const loc = Number(
            raw.location_Id ??
              raw.location_id ??
              raw.locationId ??
              raw.Location_Id,
          );
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
      const apiClient = api;

      try {
        setError(null);
        const response = await apiClient.get("/Category");
        const data = Array.isArray(response.data) ? response.data : [];
        setCategories(
          data.map((c) => ({
            category_id: c.category_id ?? c.Category_id,
            name: c.name ?? c.category_name ?? c.Category_name,
          })),
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

    const apiClient = api;

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
        categoryLimits: it.categoryLimits,
        itemLimits: it.itemLimits,
        fullPrice: it.fullPrice,
      }));

      //Group the items so all each category has its own list
      const grouped = normalized.reduce((acc, item) => {
        (acc[item.category_id] ||= []).push(item);
        return acc;
      }, {});

      setMenuItems((prev) => ({ ...prev, ...grouped }));
    } catch (err) {
      console.error(`Error fetching menu items for menu ${menu_id}:`, err);
    } finally {
      setItemsLoaded(true);
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
              (item) => item.item_id === parseInt(itemId),
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
              // Not stored; names the items in the "added to order" notification
              name: menuItem.name,
            };
          }),
    );

    return orderItems;
  };

  const handleConfirmOrder = async () => {
    if (!selectedBillId || !sessionId || !menuId) {
      setError("Please select a bill first");
      return;
    }
    if (billRequested) {
      setError(BILL_REQUESTED_MESSAGE);
      return;
    }

    const hasItems = Object.values(quantities).some((category) =>
      Object.values(category).some((quantity) => quantity > 0),
    );
    if (!hasItems) {
      setError("Please select at least one item");
      return;
    }

    setError(null);
    clearActionError();

    try {
      let orderId = pendingOrderId;

      // The saved order may have been cancelled, approved, or filled since; only reuse it
      // while it still exists, is pending, and has no items
      if (orderId) {
        const existing = await api
          .get(`/Order/${orderId}`)
          .then((res) => res.data)
          .catch(() => null);
        const reusable =
          existing &&
          String(existing.status).toLowerCase() === "pending" &&
          (existing.items?.length ?? 0) === 0;
        if (!reusable) {
          setPendingOrderId(null);
          orderId = null;
        }
      }

      if (!orderId) {
        const orderResponse = await createOrder(sessionId, selectedBillId);
        if (!orderResponse) throw new Error("Failed to create order");
        orderId = Number(orderResponse.order_Id);
        setPendingOrderId(orderId);
      }

      await addOrderItems(orderId, createOrderItems());
    } catch (error) {
      console.error("Error processing order:", error);
      setError(error.message || "Error processing order");
    }
  };

  // Combine local and hook errors
  // Without a session the info box below already shows `error` (e.g. not seated), so the
  // red alert only shows it once the guest is seated
  const displayError = (sessionId ? error : null) || actionError;

  const filteredMenuGroups = categories
    .filter(
      (category) =>
        selectedCategory === "all" || category.category_id === selectedCategory,
    )
    .map((category) => ({
      category,
      items: filterMenuItems(
        menuItems[category.category_id],
        category.category_id,
      ),
    }))
    .filter(({ items }) => items.length > 0);

  const allMenuItemCount = categories.reduce(
    (total, category) =>
      total +
      filterMenuItems(menuItems[category.category_id], category.category_id)
        .length,
    0,
  );

  return (
    <Container disableGutters maxWidth={false}>
      <Box sx={{ p: 0 }}>
        {/* Not seated: only the info message below shows, without a menu title */}
        {sessionId && (
          <Typography
            variant="h5"
            component="h1"
            sx={{
              mt: 4,
              mb: 4,
              textAlign: "left",
              width: "100%",
              fontWeight: 200,
            }}
          >
            All You Can Eat Menu
          </Typography>
        )}

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
                slotProps={{
                  input: {
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
                  },
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
                    variant={
                      selectedCategory === "all" ? "contained" : "outlined"
                    }
                    onClick={() => setSelectedCategory("all")}
                    sx={{
                      minWidth: "max-content",
                      bgcolor:
                        selectedCategory === "all" ? "#C01E2E" : "transparent",
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
            // Only once the check is done; before that sessionId is just not known yet
            sessionChecked && (
              <Alert severity="info">{error || NOT_SEATED_MESSAGE}</Alert>
            )
          )}
        </Box>

        {/* Seated, but the menu is still on its way */}
        {sessionId && !itemsLoaded && !error && (
          <Box sx={{ width: "100%", mb: 4 }}>
            <Divider sx={{ mb: 2 }} />
            <MenuListSkeleton />
          </Box>
        )}

        {sessionId && menuId && itemsLoaded && (
          <Box sx={{ width: "100%", mb: 4 }}>
            <Divider sx={{ mb: 2 }} />

            {selectedBillId && billRequested && (
              <Alert severity="info" sx={{ mb: 2 }}>
                {BILL_REQUESTED_MESSAGE}
              </Alert>
            )}

            {!selectedBillId && (
              <Alert severity="warning" sx={{ mb: 2 }}>
                Your menu is available, but there is no open bill for this
                session yet. Ask a staff member to open a bill before placing an
                order.
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
                            quantities[category.category_id]?.[item.item_id] ||
                            0
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
          bills={openBills}
          onBillChange={chooseBill}
          orderingLocked={billRequested}
          lockedMessage={BILL_REQUESTED_MESSAGE}
          itemCount={getTotalStats().itemCount}
          totalQuantity={getTotalStats().totalQuantity}
        />
      </Box>
    </Container>
  );
};

export default FullMenu;
