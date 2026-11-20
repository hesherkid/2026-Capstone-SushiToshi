import { Box, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";

const PILL_SX = { flex: 1, minHeight: 44, borderRadius: 999, textTransform: "none", fontSize: "0.95rem" };

/**
 * "Free ice cream?" on the Ready for bill confirm. The restaurant offers it at the bill stage,
 * so the guest answers Yes or No before asking for the bill. A yes goes to staff with the request,
 * and the server asks the table which flavours when they come over.
 * `value` is null until they answer, then true or false.
 */
const IceCreamChoice = ({ value, onChange }) => (
  <Box sx={{ alignSelf: "stretch", mt: 1.5 }}>
    <Typography id="ice-cream-question" sx={{ fontWeight: 600, mb: 1 }}>
      Would you like free ice cream?
    </Typography>
    <ToggleButtonGroup
      exclusive
      fullWidth
      value={value}
      // Tapping the chosen answer again keeps it, so there's always an answer once picked
      onChange={(_, next) => next !== null && onChange(next)}
      aria-labelledby="ice-cream-question"
      sx={{ gap: 1, "& .MuiToggleButtonGroup-grouped": { border: 1, borderColor: "divider", ml: 0, ...PILL_SX } }}
    >
      <ToggleButton value={true}>Yes, please</ToggleButton>
      <ToggleButton value={false}>No, thanks</ToggleButton>
    </ToggleButtonGroup>
    {value === true && (
      <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
        Your server will ask for your flavour with the bill.
      </Typography>
    )}
  </Box>
);

export default IceCreamChoice;
