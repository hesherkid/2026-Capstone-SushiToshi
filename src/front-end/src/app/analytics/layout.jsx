import { Box } from "@mui/material";
import AnalyticsNav from "@/components/analytics/AnalyticsNav";

export default function AnalyticsLayout({ children }) {
  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", px: { xs: 0, sm: 2 }, pt: 5, pb: 3 }}>
      <AnalyticsNav />
      {children}
    </Box>
  );
}
