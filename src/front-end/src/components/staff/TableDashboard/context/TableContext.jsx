import { createContext, useContext, useState, useEffect } from "react";
import { useTableData } from "../hooks/useTableData";

const TableContext = createContext();

export const useTable = () => {
  const context = useContext(TableContext);
  if (!context) {
    throw new Error("useTable must be used within a TableProvider");
  }
  return context;
};

export const TableProvider = ({ children }) => {
  const tableData = useTableData();
  const [dialogState, setDialogState] = useState({
    type: null,
    data: null,
  });
  const [actionError, setActionError] = useState(null);

  useEffect(() => {
    tableData.fetchAllData();
  }, []);

  const openDialog = (type, data = null) => {
    setDialogState({ type, data });
    setActionError(null);
  };

  // Leaves actionError alone so a failed confirm action can still show its error
  const closeDialog = () => {
    setDialogState({ type: null, data: null });
  };

  const refreshData = async () => {
    await tableData.fetchAllData();
  };

  return (
    <TableContext.Provider
      value={{
        ...tableData,
        dialogState,
        openDialog,
        closeDialog,
        actionError,
        setActionError,
        refreshData,
      }}
    >
      {children}
    </TableContext.Provider>
  );
};
