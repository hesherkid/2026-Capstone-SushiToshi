import { useEffect, useState } from "react";
import { Alert, MenuItem, TextField, Typography } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useSession } from "../../context/SessionContext";
import { isOpenBill } from "../SessionColumns";

const guestsLabel = (count) => `${count} guest${count === 1 ? "" : "s"}`;

/**
 * Staff merging one open bill into another, e.g. when the whole table wants one bill.
 * Everyone on it, all its orders and its guest counts move (so guests who never joined on a
 * phone move too), then the bill is cancelled.
 */
const MergeBillDialog = ({ open, sessionId, bill: openedBill, onClose }) => {
  const { sessions, mergeBill, isSaving } = useSession();
  const [intoId, setIntoId] = useState("");

  useEffect(() => {
    if (open) setIntoId("");
  }, [open]);

  // The live session, so a count a guest changed meanwhile shows up
  const session = sessions.find((s) => s.session_id === sessionId);
  const bills = session?.bills ?? [];
  const bill = bills.find((b) => b.bill_id === openedBill?.bill_id) ?? openedBill;
  const into = bills.find((b) => b.bill_id === intoId);
  const choices = bills.filter((b) => isOpenBill(b) && b.bill_id !== bill?.bill_id);
  const people = (session?.people ?? []).filter((p) => p.bill_id === bill?.bill_id);
  const name = bill?.bill_name ?? "this bill";

  // Moving the orders changes a total the guests may already have seen
  const askedForBill = into ? [bill, into].filter((b) => b?.requested_at) : [];

  const handleSubmit = async () => {
    if (await mergeBill(sessionId, bill.bill_id, intoId)) onClose();
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={`Merge ${name}`}
      subtitle={`Move everything on ${name} to another bill at the table, then cancel ${name}.`}
      onSubmit={handleSubmit}
      submitLabel="Merge Bills"
      busyLabel="Merging..."
      busy={isSaving}
      submitDisabled={!intoId}
    >
      {choices.length === 0 ? (
        <Alert severity="info">There&apos;s no other open bill at the table to merge into.</Alert>
      ) : (
        <TextField
          select
          label="Merge into"
          value={intoId}
          onChange={(event) => setIntoId(event.target.value)}
          disabled={isSaving}
          required
          fullWidth
        >
          {choices.map((choice) => (
            <MenuItem key={choice.bill_id} value={choice.bill_id}>
              {choice.bill_name}
            </MenuItem>
          ))}
        </TextField>
      )}

      {into && bill && (
        <Alert severity="info">
          <Typography variant="body2" component="div">
            {into.bill_name} goes from {guestsLabel(into.total_guests)} to{" "}
            {guestsLabel(into.total_guests + bill.total_guests)}, including anyone on {name} who
            didn&apos;t join on a phone.
          </Typography>
          <Typography variant="body2" component="div" sx={{ mt: 0.5 }}>
            {people.length > 0
              ? `${people.map((p) => p.user_name || "Guest").join(", ")} and all of ${name}'s orders move too.`
              : `All of ${name}'s orders move too.`}{" "}
            {name} is then cancelled.
          </Typography>
        </Alert>
      )}

      {askedForBill.map((b) => (
        <Alert key={b.bill_id} severity="warning">
          {b.bill_name} has asked for the bill. Merging changes the total, so show them the new bill
          summary.
        </Alert>
      ))}
    </FormDialog>
  );
};

export default MergeBillDialog;
