import { useEffect, useState } from "react";
import { Alert, Box, Checkbox, FormControlLabel, MenuItem, TextField } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useSession } from "../../context/SessionContext";
import { isOpenBill } from "../SessionColumns";

const GUEST_TYPES = [
  { value: "adult", key: "adult_Count", label: "Adult" },
  { value: "senior", key: "senior_Count", label: "Senior" },
  { value: "child", key: "child_Count", label: "Child" },
  { value: "toddler", key: "tot_Count", label: "Toddler (6 & Under)" },
];

const guestsLabel = (count) => `${count} guest${count === 1 ? "" : "s"}`;

/**
 * Staff moving someone at the table onto another open bill (or onto one, if they have none).
 * Their orders go with them by default, so their extras are charged on the new bill:
 * moving each person onto their own bill this way splits a bill. By default they also move from
 * the old bill's guest count to the new one's, as whichever kind of guest staff pick. One phone can
 * stand for a whole party, so staff can say how many guests are moving.
 */
const MoveGuestDialog = ({ open, sessionId, person, onClose }) => {
  const { sessions, moveGuest, isSaving } = useSession();
  const [billId, setBillId] = useState("");
  const [moveOrders, setMoveOrders] = useState(true);
  const [moveCount, setMoveCount] = useState(true);
  const [guestType, setGuestType] = useState("adult");
  const [guestCount, setGuestCount] = useState("1");

  // The live session, so a bill started meanwhile shows up
  const session = sessions.find((s) => s.session_Id === sessionId);
  const bills = session?.bills ?? [];
  const currentBill = bills.find((b) => b.bill_Id === person?.bill_Id);
  const targetBill = bills.find((b) => b.bill_Id === billId);
  const choices = bills.filter((b) => isOpenBill(b) && b.bill_Id !== person?.bill_Id);
  const name = person?.user_Name || "This guest";

  // Only kinds of guest the old bill has; without the breakdown, any kind
  const hasBreakdown = GUEST_TYPES.some(({ key }) => currentBill?.[key] != null);
  const guestTypes = hasBreakdown ? GUEST_TYPES.filter(({ key }) => currentBill[key] > 0) : GUEST_TYPES;
  // A closed bill's count can't change, and a bill needs at least one guest
  const canMoveCount = !!currentBill && isOpenBill(currentBill) && currentBill.total_Guests > 1 && guestTypes.length > 0;
  const movingCount = canMoveCount && moveCount;
  // A guest may have changed the old bill's count since this opened, so fall back to a kind it still has
  const type = guestTypes.find((t) => t.value === guestType) ?? guestTypes[0];
  // As many of that kind as the old bill has, leaving it at least one guest
  const maxCount = currentBill
    ? Math.max(1, Math.min(hasBreakdown ? currentBill[type?.key] ?? 0 : Infinity, currentBill.total_Guests - 1))
    : 1;
  const count = Math.min(Math.max(Number(guestCount) || 0, 0), maxCount);
  // Moving orders or counts changes a total the guests may already have seen
  const changesTotals = !!currentBill && (moveOrders || movingCount);
  const askedForBill = changesTotals ? [currentBill, targetBill].filter((b) => b?.requested_At) : [];

  useEffect(() => {
    if (!open) return;
    setBillId("");
    setMoveOrders(true);
    setMoveCount(true);
    setGuestType(guestTypes[0]?.value ?? "adult");
    setGuestCount("1");
    // Only on opening, so a dashboard refresh doesn't undo staff's choices
  }, [open]);

  const handleSubmit = async () => {
    const moved = movingCount ? type.value : null;
    if (await moveGuest(sessionId, person.participant_Id, billId, moveOrders, moved, count)) onClose();
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={`Move ${name}`}
      subtitle={currentBill ? `${name} is on ${currentBill.bill_Name}.` : `${name} isn't on a bill yet.`}
      onSubmit={handleSubmit}
      submitLabel="Move"
      busyLabel="Moving..."
      busy={isSaving}
      submitDisabled={!billId || (movingCount && count < 1)}
    >
      {choices.length === 0 ? (
        <Alert severity="info">
          There&apos;s no other open bill to move them to. Start a New Bill for the table first.
        </Alert>
      ) : (
        <TextField
          select
          label="Move to"
          value={billId}
          onChange={(event) => setBillId(event.target.value)}
          disabled={isSaving}
          required
          fullWidth
        >
          {choices.map((bill) => (
            <MenuItem key={bill.bill_Id} value={bill.bill_Id}>
              {bill.bill_Name}
            </MenuItem>
          ))}
        </TextField>
      )}

      {currentBill && (
        <FormControlLabel
          control={
            <Checkbox
              checked={moveOrders}
              onChange={(event) => setMoveOrders(event.target.checked)}
              disabled={isSaving}
            />
          }
          label={`Take ${name}'s orders with them, so their extras are charged on the new bill`}
        />
      )}

      {currentBill && (
        <FormControlLabel
          control={
            <Checkbox
              checked={movingCount}
              onChange={(event) => setMoveCount(event.target.checked)}
              disabled={isSaving || !canMoveCount}
            />
          }
          label={
            canMoveCount
              ? "Change both bills' guest counts"
              : `${name} can't come off ${currentBill.bill_Name}'s guest count, since it's the only guest or the bill is closed`
          }
        />
      )}

      {movingCount && (
        <Box sx={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 2 }}>
          <TextField
            select
            label="Kind of guest"
            value={type.value}
            onChange={(event) => setGuestType(event.target.value)}
            disabled={isSaving}
            fullWidth
          >
            {guestTypes.map(({ value, label }) => (
              <MenuItem key={value} value={value}>
                {label}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="How many"
            type="number"
            value={guestCount}
            // No leading zeros
            onChange={(event) => setGuestCount(event.target.value.replace(/^0+(?=\d)/, ""))}
            helperText={maxCount > 1 ? `Up to ${maxCount}` : undefined}
            slotProps={{ htmlInput: { min: 1, max: maxCount, inputMode: "numeric", pattern: "[0-9]*" } }}
            disabled={isSaving || maxCount <= 1}
          />
        </Box>
      )}

      {currentBill &&
        (movingCount ? (
          targetBill && (
            <Alert severity="info">
              {currentBill.bill_Name} goes from {guestsLabel(currentBill.total_Guests)} to{" "}
              {guestsLabel(currentBill.total_Guests - count)}, and {targetBill.bill_Name} from{" "}
              {guestsLabel(targetBill.total_Guests)} to {guestsLabel(targetBill.total_Guests + count)}.
            </Alert>
          )
        ) : (
          <Alert severity="info">
            Both bills keep their guest counts. Check them afterwards with Edit Guests.
          </Alert>
        ))}

      {askedForBill.map((bill) => (
        <Alert key={bill.bill_Id} severity="warning">
          {bill.bill_Name} has asked for the bill. This changes its total, so show them the new bill summary.
        </Alert>
      ))}
    </FormDialog>
  );
};

export default MoveGuestDialog;
