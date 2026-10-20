import { Box, Paper, Typography } from "@mui/material";
import Tags from "@/components/Tags";
import QuantityStepper from "@/components/menu/QuantityStepper";
import MaterialSymbol from "@/components/common/MaterialSymbol";

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
  const limits = limitText(item);

  const inCart = quantity > 0;

  return (
    <Paper
      variant="outlined"
      sx={{
        overflow: "hidden",
        borderRadius: 3,
        // Picked items stand out while scrolling the menu
        borderColor: inCart ? "primary.main" : "divider",
        boxShadow: inCart ? "0 0 0 1px #C01E2E" : "none",
        transition: "border-color 160ms ease, box-shadow 160ms ease",
        textAlign: "left",
      }}
    >
      <Box
        sx={{ display: "flex", gap: { xs: 1.5, sm: 2 }, width: "100%", p: 1.5 }}
      >
        {/* Square photo, or a plain tile when the item has none */}
        <Box
          sx={{
            width: { xs: 104, sm: 136 },
            height: { xs: 104, sm: 136 },
            flexShrink: 0,
            borderRadius: 2.5,
            overflow: "hidden",
            bgcolor: "grey.100",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          {item.item_image_url ? (
            <Box
              component="img"
              src={item.item_image_url}
              alt={item.name}
              loading="lazy"
              sx={{ width: "100%", height: "100%", objectFit: "cover" }}
            />
          ) : (
            <MaterialSymbol
              name="restaurant"
              size={36}
              sx={{ color: "text.disabled" }}
            />
          )}
        </Box>

        <Box
          sx={{
            flex: 1,
            minWidth: 0,
            display: "flex",
            flexDirection: "column",
          }}
        >
          <Typography
            component="h3"
            sx={{
              fontWeight: 700,
              fontSize: { xs: "1rem", sm: "1.05rem" },
              lineHeight: 1.3,
              overflowWrap: "anywhere",
              display: "-webkit-box",
              WebkitLineClamp: 2,
              WebkitBoxOrient: "vertical",
              overflow: "hidden",
            }}
          >
            {item.name}
          </Typography>

          {item.description && (
            <Typography
              variant="body2"
              color="text.secondary"
              sx={{
                mt: 0.5,
                fontSize: "0.8rem",
                lineHeight: 1.45,
                display: "-webkit-box",
                WebkitLineClamp: 2,
                WebkitBoxOrient: "vertical",
                overflow: "hidden",
              }}
            >
              {item.description}
            </Typography>
          )}

          <Tags tags={item.tags} />
          {limits && (
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ mt: 1.5, lineHeight: 1.35 }}
            >
              {limits}
            </Typography>
          )}
          <Box
            sx={{
              mt: "auto",
              pt: 1.25,
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: 1,
            }}
          >
            {item.is_add_on ? (
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ fontWeight: 600 }}
              >
                Add-on{" "}
                <Box
                  component="span"
                  sx={{ ml: 0.5, fontWeight: 700, color: "primary.main" }}
                >
                  {money(item.price)}
                </Box>
              </Typography>
            ) : (
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ fontWeight: 600 }}
              >
                Included
              </Typography>
            )}

            {showQuantityControls ? (
              <QuantityStepper
                itemName={item.name}
                quantity={quantity}
                onDecrease={() =>
                  onQuantityChange(categoryId, item.item_id, -1)
                }
                onIncrease={() => onQuantityChange(categoryId, item.item_id, 1)}
              />
            ) : (
              <Typography
                variant="body2"
                sx={{
                  px: 1.5,
                  py: 0.75,
                  borderRadius: 1,
                  bgcolor: "grey.100",
                  fontWeight: 700,
                }}
              >
                Qty {quantity}
              </Typography>
            )}
          </Box>
        </Box>
      </Box>
    </Paper>
  );
};

export default MenuItemCard;
