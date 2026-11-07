import { Box } from "@mui/material";

const SkipLink = ({ href = "#main-content", children = "Skip to content" }) => (
  <Box
    component="a"
    href={href}
    sx={{
      position: "fixed",
      top: 8,
      left: 8,
      zIndex: (theme) => theme.zIndex.tooltip + 1,
      px: 2,
      py: 1,
      borderRadius: 1,
      bgcolor: "background.paper",
      color: "primary.main",
      fontWeight: 600,
      textDecoration: "underline",
      boxShadow: 3,
      transform: "translateY(-200%)",
      "&:focus": {
        transform: "translateY(0)",
        outline: (theme) => `2px solid ${theme.palette.primary.main}`,
        outlineOffset: "2px",
      },
    }}
  >
    {children}
  </Box>
);

export default SkipLink;
