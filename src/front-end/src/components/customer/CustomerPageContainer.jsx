import { Box } from "@mui/material";

// Matches the admin pages' centred 1200px column, with no extra padding on phones
// since guests mostly order from their own mobile
const CustomerPageContainer = ({ children, sx }) => (
  <Box
    sx={{
      width: "100%",
      maxWidth: 1200,
      mx: "auto",
      px: { xs: 0, sm: 3 },
      py: { xs: 0, sm: 3 },
      ...sx,
    }}
  >
    {children}
  </Box>
);

export default CustomerPageContainer;
