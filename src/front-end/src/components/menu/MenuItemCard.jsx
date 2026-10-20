import { useEffect, useRef, useState } from "react";
import { Box, Button, Paper, Typography } from "@mui/material";
import Tags from "@/components/Tags";

const money = (value) => `$${Number(value).toFixed(2)}`;

export const limitText = (item) => {
  const parts = [];
  const cat = item.categoryLimits ?? {};
  const own = item.itemLimits ?? {};

  // Per order (whole table, each order): always blocks
  if (own.perOrder) parts.push(`${own.perOrder} per order`);
  if (cat.perOrder)
    parts.push(
      `${cat.perOrder} per order across all ${item.category ?? "items in this section"}`,
    );

  // Per person (whole dining session): the tighter one is what the guest hits first
  const perPerson = [own.adult, cat.adult].filter((n) => n > 0);
  if (perPerson.length) {
    const limit = Math.min(...perPerson);
    // Charge applies when the limit that binds is set to Charge (e.g. Hot Stone)
    const binding = own.adult === limit ? own : cat;
    const charges =
      binding.overLimitBehavior === "Charge" && item.fullPrice > 0;
    parts.push(
      charges
        ? `${limit} per person, then ${money(item.fullPrice)} each`
        : `Limit ${limit} per person`,
    );
  }

  return parts.join(" · ");
};

const MenuItemCard = ({
  item,
  categoryId,
  quantity,
  onQuantityChange,
  showQuantityControls = true,
}) => {
  const [translateX, setTranslateX] = useState(0);
  const touchStartX = useRef(null);
  const touchStartY = useRef(null);
  const elementRef = useRef(null);
  const limits = limitText(item);

  useEffect(() => {
    if (!showQuantityControls) return;

    const element = elementRef.current;
    if (!element) return;

    const handleTouchStart = (event) => {
      touchStartX.current = event.touches[0].clientX;
      touchStartY.current = event.touches[0].clientY;
    };

    const handleTouchMove = (event) => {
      if (!touchStartX.current || !touchStartY.current) return;

      const deltaX = event.touches[0].clientX - touchStartX.current;
      const deltaY = event.touches[0].clientY - touchStartY.current;

      if (Math.abs(deltaX) > Math.abs(deltaY)) {
        event.preventDefault();
        setTranslateX(Math.min(Math.max(deltaX, -75), 75));
      }
    };

    const handleTouchEnd = (event) => {
      if (!touchStartX.current) return;

      const deltaX = event.changedTouches[0].clientX - touchStartX.current;
      if (Math.abs(deltaX) >= 50) {
        onQuantityChange(categoryId, item.item_id, deltaX > 0 ? 1 : -1);
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
  }, [categoryId, item.item_id, onQuantityChange, showQuantityControls]);

  return (
    <Paper
      elevation={1}
      sx={{
        display: "flex",
        overflow: "hidden",
        minHeight: { xs: 180, sm: 188 },
        transition: "border-color 160ms ease, box-shadow 160ms ease",
      }}
    >
      <div
        ref={elementRef}
        style={{
          transform: `translateX(${translateX}px)`,
          transition: translateX === 0 ? "transform 0.2s ease-out" : "none",
          touchAction: "pan-y pinch-zoom",
          width: "100%",
          display: "flex",
        }}
      >
        <Box sx={{ display: "flex", width: "100%", minHeight: "inherit" }}>
          <Box
            sx={{
              width: { xs: 152, sm: 220 },
              minWidth: { xs: 152, sm: 220 },
              minHeight: { xs: 156, sm: 188 },
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              p: 1,
              position: "relative",
            }}
          >
            <Box
              component="img"
              src={
                item.item_image_url ||
                "https://via.placeholder.com/220x188?text=No+Image"
              }
              alt={item.name}
              sx={{
                width: "100%",
                height: { xs: 108, sm: 148 },
                objectFit: "cover",
                borderRadius: 1,
              }}
            />
            {!item.is_add_on && (
              <Typography
                variant="caption"
                sx={{
                  position: "absolute",
                  top: 12,
                  left: 12,
                  px: 1,
                  py: 0.5,
                  border: "1px solid",
                  borderColor: "divider",
                  borderRadius: 1,
                  backgroundColor: "rgba(255, 255, 255, 0.94)",
                  color: "text.primary",
                  fontWeight: 700,
                  lineHeight: 1.2,
                }}
              >
                Included
              </Typography>
            )}
          </Box>

          <Box
            sx={{
              flex: 1,
              display: "flex",
              flexDirection: "column",
              p: { xs: 1.5, sm: 2 },
              minWidth: 0,
              gap: 1.25,
              justifyContent: "space-between",
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
                  fontWeight: 700,
                  fontSize: { xs: "1rem", sm: "1.1rem" },
                  lineHeight: 1.3,
                  flex: 1,
                  mr: 1,
                  textAlign: "left",
                  overflowWrap: "anywhere",
                }}
              >
                {item.name}
              </Typography>
              {item.is_add_on && (
                <Typography
                  variant="body2"
                  sx={{
                    color: "#C01E2E",
                    whiteSpace: "nowrap",
                    fontWeight: 700,
                    fontSize: "0.85rem",
                    px: 1,
                    py: 0.5,
                    borderRadius: 1,
                  }}
                >
                  {money(item.price)}
                </Typography>
              )}
            </Box>

            <Typography
              variant="body2"
              color="text.secondary"
              align="left"
              sx={{
                fontSize: "0.75rem",
                lineHeight: 1.45,
                display: "-webkit-box",
                WebkitLineClamp: 2,
                WebkitBoxOrient: "vertical",
                overflow: "hidden",
              }}
            >
              {item.description}
            </Typography>

            {/* Ordering limits, e.g. "1 per order" or "1 per person, then $5.00 each" */}
            {limits && (
              <Typography
                variant="caption"
                align="left"
                sx={{ color: "#C01E2E", fontWeight: 600, lineHeight: 1.3 }}
              >
                {limits}
              </Typography>
            )}

            <Tags tags={item.tags} size="m" />

            {showQuantityControls ? (
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "flex-end",
                  gap: 1,
                  width: "100%",
                  mt: 0.5,
                }}
              >
                <Button
                  size="small"
                  variant="outlined"
                  aria-label={`Remove one ${item.name}`}
                  onClick={() => onQuantityChange(categoryId, item.item_id, -1)}
                  sx={{
                    minWidth: 40,
                    width: 40,
                    height: 40,
                    minHeight: 40,
                    p: 0,
                    borderColor: "#C01E2E",
                    color: "#C01E2E",
                    fontSize: "1.1rem",
                  }}
                >
                  -
                </Button>
                <Typography
                  sx={{
                    minWidth: 28,
                    textAlign: "center",
                    userSelect: "none",
                    fontWeight: 700,
                    fontSize: "0.95rem",
                  }}
                >
                  {quantity}
                </Typography>
                <Button
                  size="small"
                  variant="outlined"
                  aria-label={`Add one ${item.name}`}
                  onClick={() => onQuantityChange(categoryId, item.item_id, 1)}
                  sx={{
                    minWidth: 40,
                    width: 40,
                    height: 40,
                    minHeight: 40,
                    p: 0,
                    borderColor: "#C01E2E",
                    color: "#C01E2E",
                    fontSize: "1.1rem",
                  }}
                >
                  +
                </Button>
              </Box>
            ) : (
              <Box
                sx={{ display: "flex", justifyContent: "flex-end", mt: 0.5 }}
              >
                <Typography
                  variant="body2"
                  sx={{
                    px: 1.5,
                    py: 0.75,
                    borderRadius: 1,
                    bgcolor: "#f4f4f4",
                    fontWeight: 700,
                  }}
                >
                  Qty {quantity}
                </Typography>
              </Box>
            )}
          </Box>
        </Box>
      </div>
    </Paper>
  );
};

export default MenuItemCard;
