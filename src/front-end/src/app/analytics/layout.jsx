import { Box } from "@mui/material";
import AnalyticsNav from "@/components/analytics/AnalyticsNav";

export default function AnalyticsLayout({ children }) {
  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", p: 3 }}>
      <AnalyticsNav />
      {children}
    </Box>
  );
}
