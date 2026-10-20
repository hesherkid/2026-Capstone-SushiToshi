import { Box, Paper, Skeleton } from "@mui/material";

// Placeholder the same size as a MenuItemCard, shown while the menu loads
const MenuItemCardSkeleton = () => (
  <Paper variant="outlined" sx={{ display: "flex", gap: { xs: 1.5, sm: 2 }, p: 1.5, borderRadius: 3 }}>
    <Skeleton
      variant="rounded"
      sx={{ width: { xs: 104, sm: 136 }, height: { xs: 104, sm: 136 }, flexShrink: 0, borderRadius: 2.5 }}
    />
    <Box sx={{ flex: 1, display: "flex", flexDirection: "column" }}>
      <Skeleton variant="text" width="60%" sx={{ fontSize: "1.1rem" }} />
      <Skeleton variant="text" width="90%" />
      <Skeleton variant="text" width="75%" />
      <Box sx={{ mt: "auto", pt: 1.25, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Skeleton variant="text" width={56} />
        <Skeleton variant="rounded" width={104} height={36} sx={{ borderRadius: 999 }} />
      </Box>
    </Box>
  </Paper>
);

// A few placeholder cards; announced to screen readers as loading
export const MenuListSkeleton = ({ count = 4 }) => (
  <Box role="status" aria-label="Loading menu" sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
    {Array.from({ length: count }, (_, i) => (
      <MenuItemCardSkeleton key={i} />
    ))}
  </Box>
);

export default MenuItemCardSkeleton;
