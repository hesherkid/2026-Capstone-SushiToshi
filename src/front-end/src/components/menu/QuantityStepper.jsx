import { Box, IconButton, Typography } from "@mui/material";
import MaterialSymbol from "@/components/common/MaterialSymbol";

const inactive = {
  color: "action.disabled",
  cursor: "not-allowed",
  "&:hover": { backgroundColor: "transparent" },
};

// − qty + in one pill, shared by the menu cards and the Review order rows.
// At 1 the minus becomes a bin, since one more tap takes the item out; at 0 it's disabled.
// canIncrease=false (the item's limit is reached) hides the plus
const QuantityStepper = ({
  itemName,
  quantity,
  onDecrease,
  onIncrease,
  canIncrease = true,
}) => {
  const canDecrease = quantity > 0;

  return (
    <Box
      role="group"
      aria-label={`Quantity of ${itemName}`}
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
        aria-label={
          quantity === 1 ? `Remove ${itemName}` : `Remove one ${itemName}`
        }
        aria-disabled={!canDecrease}
        onClick={canDecrease ? onDecrease : undefined}
        disableRipple={!canDecrease}
        sx={canDecrease ? undefined : inactive}
      >
        <MaterialSymbol name={quantity === 1 ? "delete" : "remove"} size={20} />
      </IconButton>

      <Typography
        component="span"
        aria-live="polite"
        aria-atomic="true"
        sx={{
          minWidth: 24,
          textAlign: "center",
          fontWeight: 700,
          userSelect: "none",
        }}
      >
        {quantity}
      </Typography>

      <IconButton
        size="small"
        aria-label={
          canIncrease
            ? `Add one ${itemName}`
            : `Add one ${itemName}, limit reached`
        }
        aria-disabled={!canIncrease}
        onClick={canIncrease ? onIncrease : undefined}
        disableRipple={!canIncrease}
        sx={canIncrease ? undefined : inactive}
      >
        <MaterialSymbol name="add" size={20} />
      </IconButton>
    </Box>
  );
};

export default QuantityStepper;
