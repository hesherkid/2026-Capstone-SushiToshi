import { useEffect, useState } from "react";
import { Alert, Box, TextField, Typography } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useSession } from "../../context/SessionContext";
import { overSeatsWarning } from "../../utils/sessionHelpers";

const GUEST_FIELDS = [
  { name: "adultCount", key: "adult_Count", label: "Adults" },
  { name: "seniorCount", key: "senior_Count", label: "Seniors" },
  { name: "childCount", key: "child_Count", label: "Children" },
  { name: "totCount", key: "tot_Count", label: "Toddlers (6 & Under)" },
];

// The bill's current guests. If the breakdown is missing, count them all as adults so the total still matches.
const countsOf = (bill) => {
  const hasBreakdown = GUEST_FIELDS.some(({ key }) => bill?.[key] != null);
  return Object.fromEntries(
    GUEST_FIELDS.map(({ name, key }, index) => {
      const value = hasBreakdown ? bill[key] : index === 0 ? bill?.total_Guests : 0;
      return [name, String(value ?? 0)];
    }),
  );
};

/**
 * Staff fixing an open bill's guest counts, e.g. after a guest entered them on their phone.
 * Saving clears the bill's "Started by guest" and "Count changed by guest" flags.
 */
const EditGuestsDialog = ({ open, sessionId, bill: openedBill, onClose }) => {
  const { sessions, updateGuestCounts, isSaving } = useSession();
  // The live bill, so a guest's change since the session dialog opened shows up
  const session = sessions.find((s) => s.session_Id === sessionId);
  const bill = session?.bills?.find((b) => b.bill_Id === openedBill?.bill_Id) ?? openedBill;
  const [counts, setCounts] = useState(countsOf(bill));

  useEffect(() => {
    if (open) setCounts(countsOf(bill));
    // Only on opening, so a dashboard refresh doesn't wipe what staff are typing
  }, [open]);

  const total = GUEST_FIELDS.reduce((sum, { name }) => sum + (Number(counts[name]) || 0), 0);
  // Allowed (extra chairs), but staff confirm it by saving anyway
  const seatWarning = total > 0 ? overSeatsWarning(session, total, bill?.bill_Id) : null;

  const handleChange = (event) => {
    const { name, value } = event.target;
    // No leading zeros
    setCounts((prev) => ({ ...prev, [name]: value.replace(/^0+(?=\d)/, "") }));
  };

  const handleSubmit = async () => {
    if (await updateGuestCounts(sessionId, bill.bill_Id, counts)) onClose();
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={bill ? `Edit Guests on ${bill.bill_Name}` : "Edit Guests"}
      subtitle="The bill charges for each guest, so check this matches the party."
      onSubmit={handleSubmit}
      submitLabel={seatWarning ? "Save Anyway" : "Save Guests"}
      busyLabel="Saving..."
      busy={isSaving}
      submitDisabled={total === 0}
    >
      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2 }}>
        {GUEST_FIELDS.map(({ name, label }, index) => (
          <TextField
            key={name}
            autoFocus={index === 0}
            label={label}
            type="number"
            name={name}
            value={counts[name]}
            onChange={handleChange}
            slotProps={{
              htmlInput: { min: 0, max: 50, inputMode: "numeric", pattern: "[0-9]*" },
            }}
            disabled={isSaving}
          />
        ))}
      </Box>

      <Box sx={{ p: 2, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
        <Typography variant="h6" fontWeight="600" color={total === 0 ? "error" : undefined}>
          {total === 0 ? "At least one guest is required" : `Total Guests: ${total}`}
        </Typography>
      </Box>

      {seatWarning && <Alert severity="warning">{seatWarning}</Alert>}
    </FormDialog>
  );
};

export default EditGuestsDialog;
