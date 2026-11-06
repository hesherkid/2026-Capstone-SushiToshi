"use client";
import { useEffect, useRef, useState } from "react";
import {
  Typography,
  Box,
  Alert,
  TextField,
  InputAdornment,
  IconButton,
  Divider,
  Button,
} from "@mui/material";
import CategoryChips from "@/components/menu/CategoryChips";
import TagFilter, { menuTags, hasAllTags } from "@/components/menu/TagFilter";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import { MenuListSkeleton } from "@/components/menu/MenuItemCardSkeleton";
import useMenuSearch, { matchesSearch } from "@/components/menu/searchUtils";

// Filter pills for customers with an account, shown before the menu's tags
const FAVOURITES_FILTER = "my:favourites";
const ORDER_AGAIN_FILTER = "my:order-again";

// Hidden until it gets keyboard focus
const hiddenUntilFocus = {
  position: "absolute",
  width: "1px",
  height: "1px",
  overflow: "hidden",
  clip: "rect(0 0 0 0)",
  whiteSpace: "nowrap",
  "&:focus": {
    position: "relative",
    width: "auto",
    height: "auto",
    overflow: "visible",
    clip: "auto",
  },
  "&.Mui-focusVisible": {
    outline: (theme) => `2px solid ${theme.palette.primary.main}`,
    outlineOffset: "2px",
  },
};

/**
 * The menu's search box, filter pills, category bar and sections. Shared by the table's menu
 * (/menu/full-menu) and browsing before being seated (/menu/browse); each page draws its own
 * item cards with renderItem.
 *
 * - loading: show the skeleton in place of the sections
 * - ready: the menu's items are in, so show the filters, category bar and sections
 * - myItems: favourites and past orders from useMyItems, for the Favourites and Order again pills
 * - keepItem(item, categoryId): items to show whatever the search and filters (e.g. in the cart)
 * - notices: shown above the sections
 */
const MenuBrowser = ({
  categories,
  menuItems,
  loading,
  ready,
  myItems,
  keepItem = () => false,
  notices = null,
  renderItem,
  reviewOrderCount = 0,
  onReviewOrder,
}) => {
  const { canSave, favourites, orderedBefore } = myItems;

  const allItems = Object.values(menuItems).flat().filter(Boolean);

  const { searchTerm, setSearchTerm } = useMenuSearch(allItems);

  const handleSearchChange = (event) => {
    setSearchTerm(event.target.value);
  };

  // Pills picked in the filter row; an item has to match all of them
  const [selectedFilters, setSelectedFilters] = useState([]);
  const onThisMenu = (itemIds) =>
    allItems.some((item) => itemIds.has(item.item_id));
  const filterPills = [
    canSave &&
      onThisMenu(favourites) && {
        key: FAVOURITES_FILTER,
        name: "Favourites",
        symbol: "favorite",
      },
    canSave &&
      onThisMenu(orderedBefore) && {
        key: ORDER_AGAIN_FILTER,
        name: "Order again",
        symbol: "history",
      },
    ...menuTags(allItems),
  ].filter(Boolean);
  // A pill that's gone since it was picked (e.g. the last favourite un-hearted) no longer filters
  const activeFilters = selectedFilters.filter((key) =>
    filterPills.some((pill) => pill.key === key),
  );

  const matchesFilters = (item) =>
    activeFilters.every((key) => {
      if (key === FAVOURITES_FILTER) return favourites.has(item.item_id);
      if (key === ORDER_AGAIN_FILTER) return orderedBefore.has(item.item_id);
      return hasAllTags(item, [key]);
    });

  const filterMenuItems = (items, categoryId) => {
    if (!items) return [];

    return items.filter((item) => {
      if (keepItem(item, categoryId)) return true;

      return matchesFilters(item) && matchesSearch(item, searchTerm);
    });
  };

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
  const visibleIds = filteredMenuGroups.map(
    ({ category }) => category.category_id,
  );
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
  const searchInputRef = useRef(null);

  // active category's chip or the first chip if none is marked
  const focusCategory = () => {
    const bar = chipBarRef.current;
    const chip =
      bar?.querySelector(
        '[aria-current="true"], [aria-pressed="true"], [aria-selected="true"]',
      ) ?? bar?.querySelector("button, a");
    chip?.focus();
  };

  const focusSearch = () => searchInputRef.current?.focus();

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
  const pinnedHeight = () =>
    appBarHeight + (chipBarRef.current?.offsetHeight ?? 0);

  // Light up the section whose heading has reached the category bar
  const visibleKey = visibleIds.join(",");
  useEffect(() => {
    const followScroll = () => {
      if (jumping.current) return;
      const line = pinnedHeight() + 16;
      let current = visibleIdsRef.current[0] ?? null;
      for (const id of visibleIdsRef.current) {
        const section = sectionRefs.current[id];
        if (section && section.getBoundingClientRect().top <= line)
          current = id;
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

    const reduceMotion =
      window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches ?? false;

    window.scrollTo({
      top:
        section.getBoundingClientRect().top +
        window.scrollY -
        pinnedHeight() -
        8,
      behavior: reduceMotion ? "auto" : "smooth",
    });

    section.querySelector("h2")?.focus({ preventScroll: true });
  };

  // Add the pinned category bar to scroll-padding-top while it's on screen
  const chipBarShown = ready && visibleIds.length > 0;
  useEffect(() => {
    const bar = chipBarRef.current;
    if (!chipBarShown || !bar) return;
    const root = document.documentElement;
    const update = () =>
      root.style.setProperty("--sticky-bar-height", `${bar.offsetHeight}px`);
    update();
    const observer =
      typeof ResizeObserver !== "undefined" ? new ResizeObserver(update) : null;
    observer?.observe(bar);
    return () => {
      observer?.disconnect();
      root.style.removeProperty("--sticky-bar-height");
    };
  }, [chipBarShown]);

  return (
    <>
      <Box sx={{ width: "100%", mb: 3 }}>
        {/* Soft grey pill; a thin red ring while typing */}
        <TextField
          fullWidth
          inputRef={searchInputRef}
          placeholder="Search sushi, rolls, tags..."
          value={searchTerm}
          onChange={handleSearchChange}
          slotProps={{
            htmlInput: { "aria-label": "Search the menu" },
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <MaterialSymbol
                    name="search"
                    size={22}
                    sx={{ color: "text.secondary" }}
                  />
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

        {ready && (
          <TagFilter
            tags={filterPills}
            selected={activeFilters}
            onChange={setSelectedFilters}
          />
        )}
      </Box>

      {/* The menu is still on its way */}
      {loading && (
        <Box sx={{ width: "100%", mb: 4 }}>
          <Divider sx={{ mb: 2 }} />
          <MenuListSkeleton />
        </Box>
      )}

      {/* Pinned under the app bar; reaches the screen edges so items don't peek past it */}
      {ready && visibleIds.length > 0 && (
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

      {ready && (
        <Box sx={{ width: "100%", mb: 4 }}>
          {notices}

          {filteredMenuGroups.length === 0 ? (
            <Alert severity="info">
              {activeFilters.length > 0
                ? "No items match your search and filters."
                : "No items match your search."}
            </Alert>
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
                  tabIndex={-1}
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
                    {`${items.length} ${items.length === 1 ? "item" : "items"}`}
                  </Typography>
                </Typography>

                <Box
                  sx={{
                    display: "flex",
                    flexDirection: "column",
                    gap: 1.5,
                  }}
                >
                  {items.map((item) => renderItem(item, category))}
                </Box>

                {/* Keyboard shortcuts back up the page; hidden until focused */}
                <Box sx={{ display: "flex", gap: 1, mt: 1 }}>
                  <Button
                    size="small"
                    variant="outlined"
                    disableRipple
                    onClick={focusCategory}
                    sx={hiddenUntilFocus}
                  >
                    Back to categories
                  </Button>
                  <Button
                    size="small"
                    variant="outlined"
                    disableRipple
                    onClick={focusSearch}
                    sx={hiddenUntilFocus}
                  >
                    Back to search
                  </Button>
                  {onReviewOrder && reviewOrderCount > 0 && (
                    <Button
                      size="small"
                      variant="outlined"
                      disableRipple
                      onClick={onReviewOrder}
                      sx={hiddenUntilFocus}
                    >
                      {`Review order (${reviewOrderCount} ${reviewOrderCount === 1 ? "item" : "items"})`}
                    </Button>
                  )}
                </Box>
              </Box>
            ))
          )}
        </Box>
      )}
    </>
  );
};

export default MenuBrowser;
