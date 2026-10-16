import { createContext, useContext, useState, useCallback } from "react";
import { useSessionData } from "../hooks/useSessionData";
import { useSessionActions } from "../hooks/useSessionActions";

const SessionContext = createContext(null);

export const useSession = () => {
  const context = useContext(SessionContext);
  if (!context) {
    throw new Error("useSession must be used within SessionProvider");
  }
  return context;
};

// `live` polls sessions and service requests; the bills page uses the provider only for its dialogs
export const SessionProvider = ({ children, live = false }) => {
  const sessionData = useSessionData({ live });
  const { fetchSessions } = sessionData;
  const [dialogState, setDialogState] = useState({ type: null, data: null });
  // Kept apart from dialogState so New Bill / confirm dialogs can open on top of it
  const [selectedSessionId, setSelectedSessionId] = useState(null);
  const selectedSession =
    sessionData.sessions.find((s) => s.session_Id === selectedSessionId) ??
    null;

  const refreshData = useCallback(
    () => fetchSessions({ silent: true }),
    [fetchSessions]
  );

  // Actions with triggerSuccess refresh the session list when they succeed
  const actions = useSessionActions(refreshData);
  const { clearActionError } = actions;

  // The API helpers return null on failure (and show the error notification)
  const succeeded = (result) => result !== null && result !== undefined;

  const createSession = useCallback(
    async (menuId, locationId, tableId, tableGroupId, tableAssignmentType) => {
      const sessionData = {
        Menu_Id: menuId,
        Location_Id: locationId,
        AssignmentType: tableAssignmentType,
      };

      if (tableAssignmentType === "table" && tableId) {
        sessionData.Table_Id = tableId;
      } else if (tableAssignmentType === "tableGroup" && tableGroupId) {
        sessionData.TableGroup_Id = tableGroupId;
      }

      return succeeded(await actions.createDiningSession(sessionData));
    },
    [actions.createDiningSession]
  );

  const addTable = async (sessionId, tableId) =>
    succeeded(await actions.addTableToSession(sessionId, tableId));

  const addTableGroup = async (sessionId, tableGroupId) =>
    succeeded(await actions.addTableGroupToSession(sessionId, tableGroupId));

  const removeTable = async (sessionId, tableId) =>
    succeeded(await actions.removeTableFromSession(sessionId, tableId));

  const removeTableGroup = async (sessionId, tableGroupId) =>
    succeeded(
      await actions.removeTableGroupFromSession(sessionId, tableGroupId)
    );

  const createBill = async (sessionId, billData) =>
    succeeded(await actions.createBill(sessionId, billData));

  const closeBill = async (sessionId, billId) =>
    succeeded(await actions.closeBill(sessionId, billId));

  const endSession = async (sessionId) =>
    succeeded(await actions.closeDiningSession(sessionId));

  const openDialog = useCallback((type, data = null) => {
    setDialogState({ type, data });
  }, []);

  const closeDialog = useCallback(() => {
    setDialogState({ type: null, data: null });
    clearActionError();
  }, [clearActionError]);

  const value = {
    ...actions,
    ...sessionData,
    // isLoading is the data fetch; isSaving is an action in progress
    isLoading: sessionData.isLoading,
    isSaving: actions.isLoading,
    refreshData,

    // Dialog state
    dialogState,
    openDialog,
    closeDialog,
    selectedSession,
    openSession: setSelectedSessionId,
    closeSession: () => setSelectedSessionId(null),

    // Wrappers that return true/false
    createSession,
    addTable,
    addTableGroup,
    removeTable,
    removeTableGroup,
    createBill,
    closeBill,
    endSession,
  };

  return (
    <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
  );
};
