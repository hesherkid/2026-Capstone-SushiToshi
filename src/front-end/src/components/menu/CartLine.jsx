import { Box, Typography } from "@mui/material";
import { limitText } from "@/components/menu/MenuItemCard";
import QuantityStepper from "@/components/menu/QuantityStepper";

const money = (value) => `$${(Number(value) || 0).toFixed(2)}`;

// One row of the Review order list: just the name, what it costs, and a quantity stepper.
// The full card (photo, description, tags) is for browsing the menu
// atLimit: the cart holds as many as the limit allows, so no more adding and the note turns red
// extra: units past a limit that charges (e.g. Hot Stone past 1 per person), at extraPrice each
const CartLine = ({
  item,
  categoryId,
  quantity,
  onQuantityChange,
  atLimit = false,
  extra = 0,
  extraPrice = 0,
}) => {
  const limits = limitText(item);
  const included = quantity - extra;

  return (
    <Box sx={{ px: 2, py: 2, textAlign: "left" }}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography component="h3" sx={{ fontWeight: 600, lineHeight: 1.3 }}>
            {item.name}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
            {item.is_add_on ? (
              <>
                Add-on{" "}
                <Box component="span" sx={{ ml: 0.5, fontWeight: 600, color: "primary.main" }}>
                  {money(item.price)} each
                </Box>
              </>
            ) : extra > 0 ? (
              <>
                {included > 0 ? `${included} included + ${extra} extra` : "Extra"}{" "}
                <Box component="span" sx={{ ml: 0.5, fontWeight: 600, color: "primary.main" }}>
                  {money(extraPrice)} each
                </Box>
              </>
            ) : (
              "Included"
            )}
          </Typography>
        </Box>

        <QuantityStepper
          itemName={item.name}
          quantity={quantity}
          onDecrease={() => onQuantityChange(categoryId, item.item_id, -1)}
          onIncrease={() => onQuantityChange(categoryId, item.item_id, 1)}
          canIncrease={!atLimit}
        />
      </Box>

      {limits && (
        <Box
          sx={{
            mt: 1.5,
            px: 1.5,
            py: 0.75,
            borderRadius: 1.5,
            bgcolor: atLimit ? "rgba(192, 30, 46, 0.08)" : "grey.100",
            color: atLimit ? "error.main" : "text.secondary",
            fontSize: "0.8rem",
            fontWeight: atLimit ? 600 : 400,
            lineHeight: 1.4,
          }}
        >
          {limits}
        </Box>
      )}
    </Box>
  );
};

export default CartLine;
