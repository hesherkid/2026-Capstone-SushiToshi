import { Dialog, useMediaQuery, useTheme } from "@mui/material";

/**
 * The base for every modal in the app. Use it instead of MUI's Dialog so all modals share
 * the same width, spacing and phone layout.
 *
 * - Full width up to `maxWidth` (sm by default; md for detail views, xs for short confirms)
 * - On phones it fills the screen edge to edge, like the cart flyout, with square corners,
 *   the title at the top, the content scrolling and the buttons at the bottom
 * - One button row: 24px sides, 16px above and below, 8px between buttons. Titles and
 *   content keep MUI's 24px sides; a dialog can still override those with its own sx
 *   (e.g. an edge-to-edge list), since only the button row is set here
 *
 * Takes any other Dialog prop (open, onClose, slotProps, ...).
 */
const AppDialog = ({ maxWidth = "sm", fullWidth = true, sx, slotProps, children, ...props }) => {
  const theme = useTheme();
  const isPhone = useMediaQuery(theme.breakpoints.down("sm"));

  // The theme rounds dialog corners; edge to edge on a phone they should be square.
  // Set on the paper itself so it wins over the theme's override.
  const paperSx = slotProps?.paper?.sx;
  const paperSlot = {
    ...slotProps?.paper,
    sx: [...(Array.isArray(paperSx) ? paperSx : [paperSx]), isPhone && { borderRadius: 0 }],
  };

  return (
    <Dialog
      maxWidth={maxWidth}
      fullWidth={fullWidth}
      fullScreen={isPhone}
      sx={[
        {
          "& .MuiDialogActions-root": { px: 3, py: 2, gap: 1 },
          "& .MuiDialogActions-root > :not(style) ~ :not(style)": { ml: 0 },
        },
        ...(Array.isArray(sx) ? sx : [sx]),
      ]}
      slotProps={{ ...slotProps, paper: paperSlot }}
      {...props}
    >
      {children}
    </Dialog>
  );
};

export default AppDialog;
