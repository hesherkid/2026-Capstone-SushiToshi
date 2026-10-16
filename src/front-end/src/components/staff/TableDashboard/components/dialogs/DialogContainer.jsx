import { useTable } from "../../context/TableContext";
import NewTableDialog from "./NewTableDialog";
import EditTableDialog from "./EditTableDialog";
import NewTableGroupDialog from "./NewTableGroupDialog";
import EditTableGroupDialog from "./EditTableGroupDialog";
import AddTableToGroupDialog from "./AddTableToGroupDialog";
import ConfirmDialog from "./ConfirmDialog";

const DialogContainer = () => {
  const { dialogState } = useTable();

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
      <ConfirmDialog
        open={dialogState.type === "confirm"}
        data={dialogState.data}
      />
    </>
  );
};

export default DialogContainer;
