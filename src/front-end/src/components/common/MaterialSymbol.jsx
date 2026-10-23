import { Box } from "@mui/material";

// Google's Material Symbols icon font, loaded in app/layout.jsx.
// Only the icons listed in MATERIAL_SYMBOL_NAMES are downloaded, so add new ones there
export const MATERIAL_SYMBOL_NAMES = [
  "add",
  "arrow_back",
  "check_circle",
  "chevron_right",
  "close",
  "delete",
  "expand_more",
  "help",
  "logout",
  "menu_book_2",
  "qr_code_scanner",
  "receipt_long",
  "remove",
  "restaurant",
  "room_service",
  "search",
  "send",
  "shopping_bag",
];

// Google wants the icon names in alphabetical order
export const MATERIAL_SYMBOLS_URL =
  "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@24,400,0..1,0" +
  `&icon_names=${[...MATERIAL_SYMBOL_NAMES].sort().join(",")}&display=block`;

// `filled` switches the same icon from outlined to solid (e.g. for the current tab)
const MaterialSymbol = ({ name, filled = false, size = 24, sx }) => (
  <Box
    component="span"
    className="material-symbols-outlined"
    aria-hidden="true"
    sx={{
      fontSize: size,
      lineHeight: 1,
      userSelect: "none",
      fontVariationSettings: `"FILL" ${filled ? 1 : 0}`,
      transition: "font-variation-settings 150ms ease",
      ...sx,
    }}
  >
    {name}
  </Box>
);

export default MaterialSymbol;
