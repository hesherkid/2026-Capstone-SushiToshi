import { useCallback, useEffect, useRef, useState } from "react";
import { Box, ButtonBase } from "@mui/material";

// Sideways-scrolling row of category pills above the menu
const CategoryChips = ({ categories, selected, onSelect }) => {
  const rowRef = useRef(null);
  const chipRefs = useRef({});
  const [moreToRight, setMoreToRight] = useState(false);

  const checkOverflow = useCallback(() => {
    const row = rowRef.current;
    if (!row) return;
    setMoreToRight(row.scrollLeft + row.clientWidth < row.scrollWidth - 4);
  }, []);

  useEffect(() => {
    checkOverflow();
    window.addEventListener("resize", checkOverflow);
    return () => window.removeEventListener("resize", checkOverflow);
  }, [checkOverflow, categories]);

  useEffect(() => {
    const row = rowRef.current;
    const chip = chipRefs.current[selected];
    if (!row || !chip) return;
    row.scrollTo?.({
      left: chip.offsetLeft - (row.clientWidth - chip.offsetWidth) / 2,
      behavior: "smooth",
    });
  }, [selected]);

  const chips = categories.map((category) => ({
    id: category.category_id,
    label: category.name || `Category ${category.category_id}`,
  }));

  return (
    <Box sx={{ position: "relative" }}>
      <Box
        ref={rowRef}
        onScroll={checkOverflow}
        sx={{
          display: "flex",
          gap: 1,
          overflowX: "auto",
          scrollbarWidth: "none",
          "&::-webkit-scrollbar": { display: "none" },
        }}
      >
        {chips.map(({ id, label }) => {
          const isSelected = selected === id;
          return (
            <ButtonBase
              key={id}
              ref={(el) => {
                chipRefs.current[id] = el;
              }}
              aria-pressed={isSelected}
              onClick={() => onSelect(id)}
              sx={{
                flexShrink: 0,
                height: 36,
                px: 2,
                borderRadius: 999,
                fontSize: "0.9rem",
                fontWeight: isSelected ? 700 : 500,
                whiteSpace: "nowrap",
                bgcolor: isSelected ? "primary.main" : "grey.100",
                color: isSelected ? "common.white" : "grey.800",
                transition: "background-color 150ms ease, color 150ms ease",
                "&:hover": { bgcolor: isSelected ? "primary.dark" : "grey.200" },
                "&.Mui-focusVisible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: 2 },
              }}
            >
              {label}
            </ButtonBase>
          );
        })}
      </Box>

      {/* Fades the last chip out while more are hidden to the right */}
      {moreToRight && (
        <Box
          aria-hidden="true"
          sx={{
            position: "absolute",
            top: 0,
            right: 0,
            bottom: 0,
            width: 40,
            pointerEvents: "none",
            background: "linear-gradient(to right, rgba(255, 255, 255, 0), #ffffff)",
          }}
        />
      )}
    </Box>
  );
};

export default CategoryChips;
