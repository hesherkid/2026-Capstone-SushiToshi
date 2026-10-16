import api from "@/config/api";
import { useTable } from "../context/TableContext";

// API errors come back as a plain string or a ProblemDetails object
const errorMessage = (err, fallback) => {
  const data = err?.response?.data;
  if (typeof data === "string" && data) return data;
  return data?.title || data?.message || fallback;
};

// Row actions for the table and table group grids
export const useTableActions = () => {
  const { openDialog, refreshData, setActionError } = useTable();

  const run = async (request, fallback) => {
    setActionError(null);
    try {
      await request();
      await refreshData();
    } catch (err) {
      setActionError(errorMessage(err, fallback));
    }
  };

  const confirm = (title, message, confirmLabel, request, fallback) =>
    openDialog("confirm", {
      title,
      message,
      confirmLabel,
      onConfirm: () => run(request, fallback),
    });

  return {
    editTable: (table) => openDialog("editTable", table),

    toggleTable: (table) =>
      run(
        () => api.post(`/TableEntity/${table.table_Id}/toggle-status`),
        "Failed to update table status"
      ),

    removeTableFromGroup: (table) =>
      confirm(
        "Remove from group",
        `Remove Table ${table.table_number} from ${table.group_Name ?? "its group"}?`,
        "Remove",
        () => api.delete(`/TableEntity/${table.table_Id}/remove-from-group`),
        "Failed to remove table from group"
      ),

    deleteTable: (table) =>
      confirm(
        "Delete table",
        `Delete Table ${table.table_number}? This cannot be undone.`,
        "Delete",
        () => api.delete(`/TableEntity/${table.table_Id}`),
        "Failed to delete table"
      ),

    editGroup: (group) => openDialog("editTableGroup", group),

    addTableToGroup: (group) => openDialog("addTableToGroup", group),

    toggleGroup: (group) =>
      run(
        () => api.post(`/TableGroup/${group.tableGroup_Id}/toggle-status`),
        "Failed to update group status"
      ),

    deleteGroup: (group) =>
      confirm(
        "Delete table group",
        `Delete "${group.group_Name}"? This cannot be undone.`,
        "Delete",
        () => api.delete(`/TableGroup/${group.tableGroup_Id}`),
        "Failed to delete table group"
      ),
  };
};
