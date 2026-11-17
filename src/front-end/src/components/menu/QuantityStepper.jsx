import { useState } from "react";
import { Box, IconButton, InputBase, Typography } from "@mui/material";
import { visuallyHidden } from "@mui/utils";
import MaterialSymbol from "@/components/common/MaterialSymbol";

// A visible outline for keyboard focus
const focusRing = {
  "&.Mui-focusVisible": {
    outline: (theme) => `2px solid ${theme.palette.primary.main}`,
    outlineOffset: "2px",
  },
};

// Looks disabled but stays focusable, so focus never drops when a limit is hit
const inactiveSx = {
  color: "action.disabled",
  cursor: "not-allowed",
  "&:hover": { backgroundColor: "transparent" },
};

// − qty + in one pill, shared by the menu cards and the Review order rows.
// At 1 the minus becomes a bin, since one more tap takes the item out; at 0 it's disabled.
// canIncrease=false (the item's limit is reached) greys out the plus; it stays so focus isn't lost
// onSetQuantity: lets the guest type the quantity; it's applied on Enter or leaving the box,
// and the cart cuts it down to the item's limit. Left out, the quantity is plain text
const QuantityStepper = ({
  itemName,
  quantity,
  onDecrease,
  onIncrease,
  canIncrease = true,
  onSetQuantity,
}) => {
  const canDecrease = quantity > 0;
  // What's typed so far; null when the box isn't being edited
  const [draft, setDraft] = useState(null);

  const commit = () => {
    if (draft === null) return;
    setDraft(null);
    // An emptied box puts the quantity back
    if (draft !== "" && Number(draft) !== quantity)
      onSetQuantity(Number(draft));
  };

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
        sx={canDecrease ? focusRing : { ...inactiveSx, ...focusRing }}
      >
        <MaterialSymbol name={quantity === 1 ? "delete" : "remove"} size={20} />
      </IconButton>

      {onSetQuantity ? (
        <>
          <InputBase
            value={draft ?? String(quantity)}
            onChange={(e) =>
              setDraft(e.target.value.replace(/\D/g, "").slice(0, 2))
            }
            onFocus={(e) => e.target.select()}
            onBlur={commit}
            onKeyDown={(e) => {
              if (e.key === "Enter") e.currentTarget.blur();
              // Puts back what it was before typing
              if (e.key === "Escape") setDraft(null);
            }}
            inputProps={{
              inputMode: "numeric",
              pattern: "[0-9]*",
              "aria-label": `Quantity of ${itemName}`,
            }}
            sx={{
              width: 32,
              "& input": {
                p: 0,
                textAlign: "center",
                fontWeight: 700,
              },
              "&.Mui-focused": {
                borderRadius: 1,
                outline: (theme) => `2px solid ${theme.palette.primary.main}`,
              },
            }}
          />
          {/* The box's value changing isn't announced, so the +/- taps still are */}
          <Box
            component="span"
            aria-live="polite"
            aria-atomic="true"
            sx={visuallyHidden}
          >
            {quantity}
          </Box>
        </>
      ) : (
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
      )}

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
        sx={canIncrease ? focusRing : { ...inactiveSx, ...focusRing }}
      >
        <MaterialSymbol name="add" size={20} />
      </IconButton>
    </Box>
  );
};

export default QuantityStepper;
