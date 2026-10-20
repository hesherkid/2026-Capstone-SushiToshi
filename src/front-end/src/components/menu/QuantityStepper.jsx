import { Box, IconButton, Typography } from "@mui/material";
import MaterialSymbol from "@/components/common/MaterialSymbol";

// − qty + in one pill, shared by the menu cards and the Review order rows.
// At 1 the minus becomes a bin, since one more tap takes the item out; at 0 it's disabled.
// canIncrease=false (the item's limit is reached) hides the plus
const QuantityStepper = ({ itemName, quantity, onDecrease, onIncrease, canIncrease = true }) => (
  <Box
    sx={{
      display: "flex",
      alignItems: "center",
      flexShrink: 0,
      border: 1,
      borderColor: "divider",
      borderRadius: 999,
    }}
  >
    <IconButton
      size="small"
      aria-label={`Remove one ${itemName}`}
      onClick={onDecrease}
      disabled={quantity <= 0}
    >
      <MaterialSymbol name={quantity === 1 ? "delete" : "remove"} size={20} />
    </IconButton>
    <Typography
      sx={{ minWidth: 24, pr: canIncrease ? 0 : 1.5, textAlign: "center", fontWeight: 700, userSelect: "none" }}
    >
      {quantity}
    </Typography>
    {canIncrease && (
      <IconButton size="small" aria-label={`Add one ${itemName}`} onClick={onIncrease}>
        <MaterialSymbol name="add" size={20} />
      </IconButton>
    )}
  </Box>
);

export default QuantityStepper;
