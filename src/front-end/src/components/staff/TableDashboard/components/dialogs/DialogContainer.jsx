import { useTable } from "../../context/TableContext";
import NewTableDialog from "./NewTableDialog";
import EditTableDialog from "./EditTableDialog";
import NewTableGroupDialog from "./NewTableGroupDialog";
import EditTableGroupDialog from "./EditTableGroupDialog";
import AddTableToGroupDialog from "./AddTableToGroupDialog";
import ConfirmDialog from "@/components/common/ConfirmDialog";

const DialogContainer = () => {
  const { dialogState, closeDialog } = useTable();

  return (
    <>
      <NewTableDialog open={dialogState.type === "newTable"} />
      <EditTableDialog
        open={dialogState.type === "editTable"}
        table={dialogState.data}
      />
      <NewTableGroupDialog open={dialogState.type === "newTableGroup"} />
      <EditTableGroupDialog
        open={dialogState.type === "editTableGroup"}
        group={dialogState.data}
      />
      <AddTableToGroupDialog
        open={dialogState.type === "addTableToGroup"}
        group={dialogState.data}
      />
      {/* data: { title, message, confirmLabel, onConfirm } */}
      <ConfirmDialog
        open={dialogState.type === "confirm"}
        {...(dialogState.type === "confirm" ? dialogState.data : {})}
        onClose={closeDialog}
      />
    </>
  );
};

export default DialogContainer;
