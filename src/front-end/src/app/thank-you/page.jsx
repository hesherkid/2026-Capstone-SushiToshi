"use client";
import { Box, Typography } from "@mui/material";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import MaterialSymbol from "@/components/common/MaterialSymbol";

// Shown to customers once staff close their table's session (see VisitEndedWatcher)
const ThankYouPage = () => (
  <CustomerPageContainer sx={{ maxWidth: 560 }}>
    <Box sx={{ textAlign: "center", pt: { xs: 8, sm: 10 }, pb: 6 }}>

      <Typography variant="h4" component="h1" sx={{ mt: 2, fontWeight: 700 }}>
        Thank you!
      </Typography>

      <Typography sx={{ mt: 1.5, fontSize: "1.1rem" }}>
        Please accept our appreciation and sincere thanks for letting us serve you.
      </Typography>

      <Typography sx={{ mt: 1.5, fontSize: "1.1rem" }}>
        We hope to see you again soon.
      </Typography>

      <Typography color="text.secondary" sx={{ mt: 4 }}>
        Your table&apos;s visit has ended.
      </Typography>
    </Box>
  </CustomerPageContainer>
);

export default ThankYouPage;
