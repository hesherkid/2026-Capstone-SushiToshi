import { useSession } from "../../context/SessionContext";
import NewSessionDialog from "./NewSessionDialog";
import AddTableDialog from "./AddTableDialog";
import NewBillDialog from "./NewBillDialog";
import SessionDetailDialog from "./SessionDetailDialog";
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
