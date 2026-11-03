import { useEffect, useState } from "react";
import { Alert, Checkbox, FormControlLabel, MenuItem, TextField } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useSession } from "../../context/SessionContext";
import { isOpenBill } from "../SessionColumns";

/**
 * Staff moving someone at the table onto another open bill (or onto one, if they have none).
 * Their orders go with them by default, so their extras are charged on the new bill:
 * moving each person onto their own bill this way, then fixing the guest counts, splits a bill.
 */
const MoveGuestDialog = ({ open, sessionId, person, onClose }) => {
  const { sessions, moveGuest, isSaving } = useSession();
  const [billId, setBillId] = useState("");
  const [moveOrders, setMoveOrders] = useState(true);

  useEffect(() => {
    if (!open) return;
    setBillId("");
    setMoveOrders(true);
  }, [open]);

  // The live session, so a bill started meanwhile shows up
  const session = sessions.find((s) => s.session_Id === sessionId);
  const bills = session?.bills ?? [];
  const currentBill = bills.find((b) => b.bill_Id === person?.bill_Id);
  const choices = bills.filter((b) => isOpenBill(b) && b.bill_Id !== person?.bill_Id);
  const name = person?.user_Name || "This guest";

  const handleSubmit = async () => {
    if (await moveGuest(sessionId, person.participant_Id, billId, moveOrders)) onClose();
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
      submitDisabled={!billId}
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
        <Alert severity="info">
          Moving someone doesn&apos;t change either bill&apos;s guest count. Check both afterwards.
        </Alert>
      )}
    </FormDialog>
  );
};

export default MoveGuestDialog;
