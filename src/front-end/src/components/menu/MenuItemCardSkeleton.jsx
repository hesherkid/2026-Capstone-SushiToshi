import { Box, Paper, Skeleton } from "@mui/material";

// Placeholder the same size as a MenuItemCard, shown while the menu loads
const MenuItemCardSkeleton = () => (
  <Paper elevation={1} sx={{ display: "flex", overflow: "hidden", minHeight: { xs: 180, sm: 188 } }}>
    <Box sx={{ width: { xs: 152, sm: 220 }, minWidth: { xs: 152, sm: 220 }, p: 1 }}>
      <Skeleton variant="rounded" sx={{ width: "100%", height: { xs: 108, sm: 148 } }} />
    </Box>
    <Box sx={{ flex: 1, p: 2, display: "flex", flexDirection: "column", gap: 1 }}>
      <Skeleton variant="text" width="60%" sx={{ fontSize: "1.25rem" }} />
      <Skeleton variant="text" width="90%" />
      <Skeleton variant="text" width="75%" />
      <Box sx={{ mt: "auto", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Skeleton variant="text" width={48} />
        <Skeleton variant="rounded" width={96} height={36} />
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
