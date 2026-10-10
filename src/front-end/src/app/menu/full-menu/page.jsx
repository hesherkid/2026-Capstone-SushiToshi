"use client";
import { useEffect, useState, useRef } from "react";
import {
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Typography,
  Box,
  Button,
  Container,
  Alert,
  TextField,
  CircularProgress,
  Paper,
} from "@mui/material";
import { ChevronDown, Search } from "lucide-react";
import BillSelect from "@/components/customer/OrderDashboard/components/BillSelect";
import OrderSummary from "@/components/customer/OrderDashboard/components/OrderSummaryItem";
import useMenuSearch from "@/components/menu/searchUtils";
import Tags from "@/components/Tags";
import TrackedItem from "@/components/menu/TrackedItem";
import api from "@/config/api";
import publicApi from "@/config/publicApi";
import { useOrderActions } from "@/hooks/useOrderActions";

const SWIPE_THRESHOLD = 50;
const ANIMATION_DURATION = 300;

//Check if the logged in user is a guest.
const isGuest = () =>
  typeof window !== "undefined" && localStorage.getItem("guest") === "true";

const SwipeableItem = ({
  children,
  onSwipeLeft,
  onSwipeRight,
  threshold = 50,
  className = "",
}) => {
  const [translateX, setTranslateX] = useState(0);
  const touchStartX = useRef(null);
  const touchStartY = useRef(null);
  const elementRef = useRef(null);

  useEffect(() => {
    const element = elementRef.current;
    if (!element) return;

    const handleTouchStart = (e) => {
      touchStartX.current = e.touches[0].clientX;
      touchStartY.current = e.touches[0].clientY;
    };

    const handleTouchMove = (e) => {
      if (!touchStartX.current || !touchStartY.current) return;

      const touchX = e.touches[0].clientX;
      const touchY = e.touches[0].clientY;
      const deltaX = touchX - touchStartX.current;
      const deltaY = touchY - touchStartY.current;

      if (Math.abs(deltaX) > Math.abs(deltaY)) {
        e.preventDefault();
        const limitedDeltaX = Math.min(Math.max(deltaX, -75), 75);
        setTranslateX(limitedDeltaX);
      }
    };

    const handleTouchEnd = (e) => {
      if (!touchStartX.current) return;

      const deltaX = e.changedTouches[0].clientX - touchStartX.current;

      if (Math.abs(deltaX) >= threshold) {
        if (deltaX > 0) {
          onSwipeRight?.();
        } else {
          onSwipeLeft?.();
        }
      }

      setTranslateX(0);
      touchStartX.current = null;
      touchStartY.current = null;
    };

    element.addEventListener("touchstart", handleTouchStart, { passive: true });
    element.addEventListener("touchmove", handleTouchMove, { passive: false });
    element.addEventListener("touchend", handleTouchEnd, { passive: true });

    return () => {
      element.removeEventListener("touchstart", handleTouchStart);
      element.removeEventListener("touchmove", handleTouchMove);
      element.removeEventListener("touchend", handleTouchEnd);
    };
  }, [onSwipeLeft, onSwipeRight, threshold]);

  return (
    <div
      ref={elementRef}
      className={className}
      style={{
        transform: `translateX(${translateX}px)`,
        transition: translateX === 0 ? "transform 0.2s ease-out" : "none",
        touchAction: "pan-y pinch-zoom",
        width: "100%",
      }}
    >
      {children}
    </div>
  );
};

const FullMenu = () => {
  const [categories, setCategories] = useState([]);
  const [menuItems, setMenuItems] = useState({});
  const [quantities, setQuantities] = useState({});
  const [sessionId, setSessionId] = useState(null);
  const [error, setError] = useState(null);
  const [selectedBillId, setSelectedBillId] = useState("");
  const [menuId, setMenuId] = useState("");
  const [orderSuccess, setOrderSuccess] = useState(false);

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
          "/DiningSession/participants/active-session-id",
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
    //Session ID can only be 1 or above
    if (!sessionId || sessionId < 1) {
      return;
    }

    const fetchMenu = async () => {
      const apiClient = isGuest() ? publicApi : api;
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

  //When you have the menuID call to get current menu
  useEffect(() => {
    if (!menuId) return;
    fetchMenuItems(null, menuId);
  }, [menuId]);

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
            name: c.name ?? c.Category_name,
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

  const fetchMenuItems = async (categoryId, menu_id) => {
    //Make sure the menu ID is valid or if we already loaded the menu items
    if (!menu_id || menuItems[categoryId]) return;

    const apiClient = isGuest() ? publicApi : api;

    try {
      //Call GetItemsByMenu and check if it's in an array
      const response = await apiClient.get(`/Menu/${menu_id}/item`);
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
      const orderResponse = await createOrder(sessionId, selectedBillId);

      if (!orderResponse) throw new Error("Failed to create order");

      const newOrderId = Number(orderResponse.order_Id);

      const orderItems = createOrderItems();

      await addOrderItems(newOrderId, orderItems);
    } catch (error) {
      console.error("❌ Error processing order:", error);
      setError(error.message || "Error processing order");
    }
  };

  // Combine local and hook errors
  const displayError = error || actionError;

  return (
    <Container disableGutters maxWidth={false}>
      <Box sx={{ p: 0 }}>
        <Typography
          variant="h4"
          component="h1"
          sx={{ mt: 4, mb: 4, textAlign: "center", width: "100%" }}
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

        <Box sx={{ width: "100%" }} mb={3}>
          {sessionId ? (
            <>
              <BillSelect
                session_id={sessionId}
                value={selectedBillId}
                onChange={handleBillChange}
                sx={{ width: "100%", mb: 2 }}
                message="Select Bill To Place Order"
              />
              {selectedBillId && (
                <TextField
                  fullWidth
                  placeholder="Search items..."
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
              )}
            </>
          ) : (
            <Alert severity="info">No active session found</Alert>
          )}
        </Box>

        {selectedBillId && (
          <Box sx={{ width: "100%", mb: 4 }}>
            {categories.map((category) => {
              const filteredItems = filterMenuItems(
                menuItems[category.category_id],
                category.category_id,
              );

              const stats = getSelectedItemsStats(category.category_id);
              const visibleCount = filteredItems.length;

              // Skip categories with no visible items
              if (visibleCount === 0 && stats.itemCount === 0) {
                return null;
              }

              return (
                <Accordion
                  key={`category-${category.category_id}`}
                  onChange={() =>
                    menuId && fetchMenuItems(category.category_id, menuId)
                  }
                  sx={{ mb: 1 }}
                >
                  <AccordionSummary
                    expandIcon={<ChevronDown />}
                    sx={{
                      "&.MuiAccordionSummary-root": {
                        backgroundColor: "#f8f9fa",
                        minHeight: "48px",
                        "&:hover": { backgroundColor: "#eeeeee" },
                        justifyContent: "center",
                      },
                    }}
                  >
                    <Box
                      sx={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        width: "100%",
                        pr: 2,
                      }}
                    >
                      <Typography variant="subtitle1" sx={{ fontWeight: 500 }}>
                        {category.name ||
                          menuItems[category.category_id]?.[0]?.category ||
                          `Category ${category.category_id}`}{" "}
                        ({visibleCount} {visibleCount === 1 ? "item" : "items"})
                      </Typography>

                      {stats.itemCount > 0 && (
                        <Typography
                          variant="subtitle2"
                          sx={{
                            color: "#C01E2E",
                            bgcolor: "#FFF3CD",
                            px: 1.5,
                            py: 0.5,
                            borderRadius: 1,
                            fontWeight: "medium",
                            textAlign: "center",
                          }}
                        >
                          {`${stats.itemCount} ${stats.itemCount === 1 ? "item" : "items"}, ${stats.totalQuantity} ${stats.totalQuantity === 1 ? "unit" : "units"}`}
                        </Typography>
                      )}
                    </Box>
                  </AccordionSummary>
                  <AccordionDetails sx={{ p: 0 }}>
                    <Box
                      sx={{
                        display: "flex",
                        flexDirection: "column",
                        gap: 0.5,
                        width: "100%",
                      }}
                    >
                      {filteredItems.map((item) => (
                        // Records how long the card is on screen (browsing behavior analytics)
                        <TrackedItem
                          key={`item-${category.category_id}-${item.item_id}`}
                          itemId={item.item_id}
                          sessionId={sessionId}
                        >
                          <Paper
                            key={`item-${category.category_id}-${item.item_id}`}
                            elevation={1}
                            sx={{
                              display: "flex",
                              width: "100%",
                              border: "1px solid",
                              borderColor: "divider",
                              borderRadius: 1,
                              overflow: "hidden",
                              minHeight: "100px",
                            }}
                          >
                            <SwipeableItem
                              key={`swipeable-${category.category_id}-${item.item_id}`}
                              onSwipeLeft={() =>
                                handleQuantityChange(
                                  category.category_id,
                                  item.item_id,
                                  -1,
                                )
                              }
                              onSwipeRight={() =>
                                handleQuantityChange(
                                  category.category_id,
                                  item.item_id,
                                  1,
                                )
                              }
                            >
                              <Box sx={{ display: "flex", width: "100%" }}>
                                <Box
                                  sx={{
                                    width: "90px",
                                    minWidth: "90px",
                                    height: "100%",
                                    position: "relative",
                                  }}
                                >
                                  <Box
                                    component="img"
                                    src={
                                      item.item_image_url ||
                                      "/images/placeholder.png"
                                    }
                                    alt={item.name}
                                    sx={{
                                      width: "100%",
                                      height: "100%",
                                      objectFit: "cover",
                                    }}
                                  />
                                </Box>

                                <Box
                                  sx={{
                                    flex: 1,
                                    display: "flex",
                                    flexDirection: "column",
                                    p: 1.5,
                                    minWidth: 0,
                                    gap: 1.5,
                                  }}
                                >
                                  <Box
                                    sx={{
                                      display: "flex",
                                      justifyContent: "space-between",
                                      alignItems: "flex-start",
                                      width: "100%",
                                    }}
                                  >
                                    <Typography
                                      variant="subtitle1"
                                      sx={{
                                        fontWeight: "medium",
                                        fontSize: "0.9rem",
                                        lineHeight: 1.2,
                                        flex: 1,
                                        mr: 1,
                                        textAlign: "left",
                                      }}
                                      noWrap
                                    >
                                      {item.name}
                                    </Typography>
                                    <Typography
                                      variant="body2"
                                      sx={{
                                        color: "text.secondary",
                                        whiteSpace: "nowrap",
                                        fontSize: "0.8rem",
                                      }}
                                    >
                                      {item.is_add_on
                                        ? `$${item.price}`
                                        : "Included"}
                                    </Typography>
                                  </Box>

                                  <Typography
                                    variant="body2"
                                    color="text.secondary"
                                    align="left"
                                    sx={{
                                      fontSize: "0.75rem",
                                      lineHeight: 1.3,
                                    }}
                                  >
                                    {item.description}
                                  </Typography>

                                  <Box
                                    sx={{
                                      display: "flex",
                                      flexWrap: "wrap",
                                      gap: 0.5,
                                    }}
                                  >
                                    <Tags item_id={item.tags} size="m" />
                                  </Box>

                                  <Box
                                    sx={{
                                      display: "flex",
                                      alignItems: "center",
                                      gap: 0.5,
                                      width: "100%",
                                    }}
                                  >
                                    <Button
                                      size="small"
                                      variant="outlined"
                                      onClick={() =>
                                        handleQuantityChange(
                                          category.category_id,
                                          item.item_id,
                                          -1,
                                        )
                                      }
                                      sx={{
                                        flex: 1,
                                        height: "30px",
                                        borderColor: "#C01E2E",
                                        color: "#C01E2E",
                                      }}
                                    >
                                      -
                                    </Button>
                                    <Typography
                                      sx={{
                                        minWidth: "24px",
                                        textAlign: "center",
                                        userSelect: "none",
                                        fontSize: "0.85rem",
                                      }}
                                    >
                                      {quantities[category.category_id]?.[
                                        item.item_id
                                      ] || 0}
                                    </Typography>
                                    <Button
                                      size="small"
                                      variant="outlined"
                                      onClick={() =>
                                        handleQuantityChange(
                                          category.category_id,
                                          item.item_id,
                                          1,
                                        )
                                      }
                                      sx={{
                                        flex: 1,
                                        height: "30px",
                                        borderColor: "#C01E2E",
                                        color: "#C01E2E",
                                      }}
                                    >
                                      +
                                    </Button>
                                  </Box>
                                </Box>
                              </Box>
                            </SwipeableItem>
                          </Paper>
                        </TrackedItem>
                      ))}
                    </Box>
                  </AccordionDetails>
                </Accordion>
              );
            })}

            {orderSuccess && (
              <Alert severity="success" sx={{ mb: 2 }}>
                Order placed successfully!
              </Alert>
            )}

            {/* Order Summary */}
            {getTotalStats().itemCount > 0 && (
              <Paper elevation={2} sx={{ mb: 3, overflow: "hidden" }}>
                <Box sx={{ p: 2 }}>
                  <OrderSummary
                    categories={categories}
                    quantities={quantities}
                    menuItems={menuItems}
                    onQuantityChange={handleQuantityChange}
                  />
                </Box>
              </Paper>
            )}

            {/* Confirm Order Button */}
            <Button
              variant="contained"
              fullWidth
              size="large"
              onClick={handleConfirmOrder}
              disabled={
                isLoading || !selectedBillId || getTotalStats().itemCount === 0
              }
              sx={{
                bgcolor: "#C01E2E",
                "&:hover": {
                  bgcolor: "#A01725",
                },
                py: 1.5,
                fontSize: "1.2rem",
              }}
            >
              {isLoading ? (
                <CircularProgress size={24} color="inherit" />
              ) : (
                `Confirm Order ${
                  getTotalStats().itemCount > 0
                    ? `(${getTotalStats().itemCount} items`
                    : ""
                }${
                  getTotalStats().itemCount > 0 &&
                  getTotalStats().totalQuantity > 0
                    ? ", "
                    : ""
                }${
                  getTotalStats().totalQuantity > 0
                    ? `${getTotalStats().totalQuantity} units)`
                    : ""
                }`
              )}
            </Button>
          </Box>
        )}
      </Box>
    </Container>
  );
};

export default FullMenu;
