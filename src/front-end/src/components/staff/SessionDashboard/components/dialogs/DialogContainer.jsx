import { useSession } from "../../context/SessionContext";
import NewSessionDialog from "./NewSessionDialog";
import AddTableDialog from "./AddTableDialog";
import NewBillDialog from "./NewBillDialog";
import SessionDetailDialog from "./SessionDetailDialog";
import StaffOrderDialog from "./StaffOrderDialog";
import EditGuestsDialog from "./EditGuestsDialog";
import MoveGuestDialog from "./MoveGuestDialog";
import MergeBillDialog from "./MergeBillDialog";
import ConfirmDialog from "@/components/common/ConfirmDialog";

const DialogContainer = () => {
  const { dialogState, closeDialog } = useSession();
  const { type, data } = dialogState;

  return (
    <>
      <SessionDetailDialog />

      <NewSessionDialog open={type === "newSession"} onClose={closeDialog} />

      <AddTableDialog
        open={type === "addTable"}
        sessionId={data}
        onClose={closeDialog}
      />

      <NewBillDialog
        open={type === "newBill"}
        sessionId={data}
        onClose={closeDialog}
      />

      {/* data: { sessionId, billId } */}
      <StaffOrderDialog
        open={type === "staffOrder"}
        sessionId={data?.sessionId}
        billId={data?.billId}
        onClose={closeDialog}
      />

      {/* data: { sessionId, bill } */}
      <EditGuestsDialog
        open={type === "editGuests"}
        sessionId={data?.sessionId}
        bill={data?.bill}
        onClose={closeDialog}
      />

      {/* data: { sessionId, person } */}
      <MoveGuestDialog
        open={type === "moveGuest"}
        sessionId={data?.sessionId}
        person={data?.person}
        onClose={closeDialog}
      />

      {/* data: { sessionId, bill } */}
      <MergeBillDialog
        open={type === "mergeBill"}
        sessionId={data?.sessionId}
        bill={data?.bill}
        onClose={closeDialog}
      />

      {/* data: { title, message, confirmLabel, onConfirm } */}
      <ConfirmDialog
        open={type === "confirm"}
        {...(type === "confirm" ? data : {})}
        onClose={closeDialog}
      />
    </>
  );
};

export default DialogContainer;
