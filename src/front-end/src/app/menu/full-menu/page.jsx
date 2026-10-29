"use client";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Typography,
  Box,
  Alert,
  TextField,
  InputAdornment,
  IconButton,
  Divider,
} from "@mui/material";
import MenuItemCard from "@/components/menu/MenuItemCard";
import CategoryChips from "@/components/menu/CategoryChips";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import ReviewOrderButton from "@/components/menu/ReviewOrderButton";
import { MenuListSkeleton } from "@/components/menu/MenuItemCardSkeleton";
import TrackedItem from "@/components/menu/TrackedItem";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import useMenuSearch from "@/components/menu/searchUtils";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { NOT_SEATED_MESSAGE } from "@/services/tableSession";
import {
  useTableOrder,
  BILL_REQUESTED_MESSAGE,
  REVIEW_ORDER_PATH,
} from "@/contexts/TableOrderContext";

// Browsing only: the cart and ordering live on the Review order page (/menu/review).
// Session, menu, bills and cart come from TableOrderContext, so they survive going there and back
const FullMenu = () => {
  const router = useRouter();
  const {
    sessionId,
    sessionChecked,
    error,
    setError,
    refresh,
    categories,
    menuItems,
    itemsLoaded,
    menuId,
    selectedBillId,
    billRequested,
    quantities,
    changeQuantity,
    limitStatus,
    cartCount,
    menuScrollYRef,
    reviewOpenedFromMenuRef,
  } = useTableOrder();

  // Re-check the table and bills each visit; the menu already loaded shows meanwhile
  useEffect(() => {
    refresh();
  }, [refresh]);

  // Keep bills and limits current while the guest browses (others at the table order too)
  useAutoRefresh(refresh);

  // Put the guest back where they were on the menu after Review order.
  // Read once on arrival, before scrolling to the top of this page can overwrite it
  const returnToY = useRef(0);
  useEffect(() => {
    returnToY.current = menuScrollYRef.current;
  }, []);
  const scrollRestored = useRef(false);
  useEffect(() => {
    if (scrollRestored.current || !itemsLoaded) return;
    scrollRestored.current = true;
    if (returnToY.current > 0) {
      requestAnimationFrame(() => window.scrollTo(0, returnToY.current));
    }
  }, [itemsLoaded]);

  useEffect(() => {
    const remember = () => {
      menuScrollYRef.current = window.scrollY;
    };
    window.addEventListener("scroll", remember, { passive: true });
    return () => window.removeEventListener("scroll", remember);
  }, [menuScrollYRef]);

  // The app bar's bag also opens Review order
  const reviewOrder = () => {
    reviewOpenedFromMenuRef.current = true;
    router.push(REVIEW_ORDER_PATH);
  };

  const handleSearchChange = (event) => {
    setSearchTerm(event.target.value);
  };

  const {
    searchTerm,
    setSearchTerm,
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

      return searchTerms.every((term) => {
        return (
          item.name.toLowerCase().includes(term) ||
          (item.description && item.description.toLowerCase().includes(term)) ||
          (item.tags &&
            item.tags.some((tag) => tag.name.toLowerCase().includes(term)))
        );
      });
    });
  };

  // Without a session the info box below already shows `error` (e.g. not seated), so the
  // red alert only shows it once the guest is seated
  const displayError = sessionId ? error : null;

  // Every section, minus any the search empties
  const filteredMenuGroups = categories
    .map((category) => ({
      category,
      items: filterMenuItems(
        menuItems[category.category_id],
        category.category_id,
      ),
    }))
    .filter(({ items }) => items.length > 0);

  // ---- Category bar: pinned under the app bar, jumps to sections, follows the scroll ----
  const visibleIds = filteredMenuGroups.map(({ category }) => category.category_id);
  const visibleIdsRef = useRef(visibleIds);
  useEffect(() => {
    visibleIdsRef.current = visibleIds;
  });
  const sectionRefs = useRef({});
  const chipBarRef = useRef(null);
  const [appBarHeight, setAppBarHeight] = useState(56);
  const [activeCategory, setActiveCategory] = useState(null);
  // Set while a chip tap is scrolling the page, so the sections it passes don't light up
  const jumping = useRef(null);

  // The app bar's height depends on its logo, so measure it rather than guess
  useEffect(() => {
    const measure = () => {
      const bar = document.querySelector("header.MuiAppBar-root");
      if (bar?.offsetHeight) setAppBarHeight(bar.offsetHeight);
    };
    measure();
    window.addEventListener("resize", measure);
    return () => window.removeEventListener("resize", measure);
  }, []);

  // Space taken at the top by the app bar and the pinned category bar
  const pinnedHeight = () => appBarHeight + (chipBarRef.current?.offsetHeight ?? 0);

  // Light up the section whose heading has reached the category bar
  const visibleKey = visibleIds.join(",");
  useEffect(() => {
    const followScroll = () => {
      if (jumping.current) return;
      const line = pinnedHeight() + 16;
      let current = visibleIdsRef.current[0] ?? null;
      for (const id of visibleIdsRef.current) {
        const section = sectionRefs.current[id];
        if (section && section.getBoundingClientRect().top <= line) current = id;
      }
      setActiveCategory(current);
    };

    followScroll();
    window.addEventListener("scroll", followScroll, { passive: true });
    return () => window.removeEventListener("scroll", followScroll);
  }, [visibleKey, appBarHeight]);

  useEffect(() => () => clearTimeout(jumping.current), []);

  const jumpToCategory = (categoryId) => {
    const section = sectionRefs.current[categoryId];
    if (!section) return;

    setActiveCategory(categoryId);
    clearTimeout(jumping.current);
    const done = () => {
      clearTimeout(jumping.current);
      jumping.current = null;
    };
    // "scrollend" where the browser has it; the timer covers the rest
    window.addEventListener("scrollend", done, { once: true });
    jumping.current = setTimeout(done, 1000);

    window.scrollTo({
      top: section.getBoundingClientRect().top + window.scrollY - pinnedHeight() - 8,
      behavior: "smooth",
    });
  };

  return (
    <CustomerPageContainer>
      {/* Not seated: only the info message below shows, without a menu title */}
      {sessionId && (
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700, mt: { xs: 3, sm: 2 }, mb: 3 }}>
          All You Can Eat Menu
        </Typography>
      )}

      {/* Error Alert */}
      {displayError && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          onClose={() => setError(null)}
        >
          {displayError}
        </Alert>
      )}

      <Box sx={{ width: "100%", mb: 3 }}>
        {sessionId ? (
          <>
            {/* Soft grey pill; a thin red ring while typing */}
            <TextField
              fullWidth
              placeholder="Search sushi, rolls, tags..."
              value={searchTerm}
              onChange={handleSearchChange}
              slotProps={{
                htmlInput: { "aria-label": "Search the menu" },
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <MaterialSymbol name="search" size={22} sx={{ color: "text.secondary" }} />
                    </InputAdornment>
                  ),
                  endAdornment: searchTerm ? (
                    <InputAdornment position="end">
                      <IconButton
                        size="small"
                        aria-label="Clear search"
                        onClick={() => setSearchTerm("")}
                        edge="end"
                      >
                        <MaterialSymbol name="close" size={20} />
                      </IconButton>
                    </InputAdornment>
                  ) : null,
                },
              }}
              sx={{
                mb: 2.5,
                "& .MuiOutlinedInput-root": {
                  height: 48,
                  borderRadius: 999,
                  bgcolor: "grey.100",
                  pl: 1.5,
                  pr: 1.5,
                  transition: "background-color 150ms ease, box-shadow 150ms ease",
                  "& fieldset": { border: "none" },
                  "&:hover": { bgcolor: "grey.200" },
                  "&.Mui-focused": {
                    bgcolor: "common.white",
                    boxShadow: "0 0 0 2px #C01E2E",
                  },
                },
                "& .MuiOutlinedInput-input::placeholder": {
                  color: "text.secondary",
                  opacity: 1,
                },
              }}
            />

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

      {/* Pinned under the app bar; reaches the screen edges so items don't peek past it */}
      {sessionId && menuId && itemsLoaded && visibleIds.length > 0 && (
        <Box
          ref={chipBarRef}
          sx={{
            position: "sticky",
            top: appBarHeight,
            zIndex: (theme) => theme.zIndex.appBar - 1,
            bgcolor: "background.paper",
            mx: { xs: -1.5, sm: -5 },
            px: { xs: 1.5, sm: 5 },
            py: 1.25,
            mb: 2,
            borderBottom: "1px solid",
            borderColor: "divider",
          }}
        >
          <CategoryChips
            categories={filteredMenuGroups.map(({ category }) => category)}
            selected={activeCategory}
            onSelect={jumpToCategory}
          />
        </Box>
      )}

      {sessionId && menuId && itemsLoaded && (
        <Box sx={{ width: "100%", mb: 4 }}>

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

          {filteredMenuGroups.length === 0 ? (
            <Alert severity="info">No items match your search.</Alert>
          ) : (
            filteredMenuGroups.map(({ category, items }) => (
              <Box
                key={`group-${category.category_id}`}
                id={`category-${category.category_id}`}
                ref={(el) => {
                  sectionRefs.current[category.category_id] = el;
                }}
                sx={{ mb: 4 }}
              >
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
                        onQuantityChange={changeQuantity}
                        atLimit={
                          limitStatus(category.category_id, item.item_id).atLimit
                        }
                        // The bill's been asked for, so nothing more can be ordered onto it
                        showQuantityControls={!billRequested}
                      />
                    </TrackedItem>
                  ))}
                </Box>
              </Box>
            ))
          )}
        </Box>
      )}

      {/* Ordering is over once the bill's been asked for */}
      {!billRequested && <ReviewOrderButton count={cartCount} onClick={reviewOrder} />}
    </CustomerPageContainer>
  );
};

export default FullMenu;
