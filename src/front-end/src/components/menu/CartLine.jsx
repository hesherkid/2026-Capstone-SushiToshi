import { Box, Typography } from "@mui/material";
import { limitText } from "@/components/menu/MenuItemCard";
import QuantityStepper from "@/components/menu/QuantityStepper";

const money = (value) => `$${(Number(value) || 0).toFixed(2)}`;

// One row of the Review order list: just the name, what it costs, and a quantity stepper.
// The full card (photo, description, tags) is for browsing the menu
const CartLine = ({ item, categoryId, quantity, onQuantityChange }) => {
  const limits = limitText(item);

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
        />
      </Box>

      {limits && (
        <Box
          sx={{
            mt: 1.5,
            px: 1.5,
            py: 0.75,
            borderRadius: 1.5,
            bgcolor: "grey.100",
            color: "text.secondary",
            fontSize: "0.8rem",
            fontWeight: 400,
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
