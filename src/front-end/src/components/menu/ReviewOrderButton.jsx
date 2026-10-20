import { Box, Button } from "@mui/material";
import { NAV_HEIGHT } from "@/components/customer/CustomerBottomNav";

const BUTTON_HEIGHT = 52;
const GAP = 12;

// Sticky "Review order" button on the menu: the way to the Review order page, with the
// cart count in a white circle.
// Floats bottom-right, just above the customer tab bar on phones
const ReviewOrderButton = ({ count, onClick }) => {
  if (count <= 0) return null;

  const itemsLabel = `${count} ${count === 1 ? "item" : "items"}`;

  return (
    <>
      {/* Room at the end of the menu so the button never covers the last items */}
      <Box sx={{ height: BUTTON_HEIGHT + GAP * 2 }} />

      <Box
        sx={{
          position: "fixed",
          left: 0,
          right: 0,
          bottom: {
            xs: `calc(${NAV_HEIGHT + GAP}px + env(safe-area-inset-bottom))`,
            md: 24,
          },
          px: { xs: 1.5, md: 3 },
          display: "flex",
          justifyContent: "flex-end",
          zIndex: (theme) => theme.zIndex.appBar,
          pointerEvents: "none",
        }}
      >
        <Button
          variant="contained"
          color="primary"
          onClick={onClick}
          aria-label={`Review order, ${itemsLabel}`}
          sx={{
            pointerEvents: "auto",
            height: BUTTON_HEIGHT,
            borderRadius: 999,
            pl: 3,
            pr: 1.5,
            gap: 1.5,
            fontSize: "0.9rem",
            boxShadow: "0 6px 16px rgba(192, 30, 46, 0.3)",
          }}
        >
          Review order
          <Box
            component="span"
            sx={{
              minWidth: 28,
              height: 28,
              px: 1,
              borderRadius: 999,
              bgcolor: "common.white",
              color: "primary.main",
              fontWeight: 700,
              fontSize: "0.78rem",
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            {count > 99 ? "99+" : count}
          </Box>
        </Button>
      </Box>
    </>
  );
};

export default ReviewOrderButton;
