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
    sessionData.sessions.find((s) => s.session_id === selectedSessionId) ??
    null;

  const refreshData = useCallback(
    () => fetchSessions({ silent: true }),
    [fetchSessions],
  );

  // Actions with triggerSuccess refresh the session list when they succeed
  const actions = useSessionActions(refreshData);
  const { clearActionError } = actions;

  // The API helpers return null on failure (and show the error notification)
  const succeeded = (result) => result !== null && result !== undefined;

  const createSession = useCallback(
    async (menuId, locationId, tableId, tableGroupId, tableAssignmentType) => {
      const sessionData = {
        menu_id: menuId,
        location_id: locationId,
        assignment_type: tableAssignmentType,
      };

      if (tableAssignmentType === "table" && tableId) {
        sessionData.table_id = tableId;
      } else if (tableAssignmentType === "tableGroup" && tableGroupId) {
        sessionData.table_group_id = tableGroupId;
      }

      return succeeded(await actions.createDiningSession(sessionData));
    },
    [actions.createDiningSession],
  );

  const addTable = async (sessionId, tableId) =>
    succeeded(await actions.addTableToSession(sessionId, tableId));

  const addTableGroup = async (sessionId, tableGroupId) =>
    succeeded(await actions.addTableGroupToSession(sessionId, tableGroupId));

  const removeTable = async (sessionId, tableId) =>
    succeeded(await actions.removeTableFromSession(sessionId, tableId));

  const removeTableGroup = async (sessionId, tableGroupId) =>
    succeeded(
      await actions.removeTableGroupFromSession(sessionId, tableGroupId),
    );

  const createBill = async (sessionId, billData) =>
    succeeded(await actions.createBill(sessionId, billData));

  const closeBill = async (sessionId, billId) =>
    succeeded(await actions.closeBill(sessionId, billId));

  const updateGuestCounts = async (sessionId, billId, counts) =>
    succeeded(await actions.updateGuestCounts(sessionId, billId, counts));

  const lockGuestCounts = async (sessionId, billId, locked) =>
    succeeded(await actions.lockGuestCounts(sessionId, billId, locked));

  const mergeBill = async (sessionId, billId, intoBillId) =>
    succeeded(await actions.mergeBill(sessionId, billId, intoBillId));

  // newBill: starts a new bill split off their current bill
  const moveGuest = async (
    sessionId,
    participantId,
    billId,
    moveOrders,
    guestType,
    guestCount,
    newBill = false,
  ) =>
    succeeded(
      await actions.moveGuest(
        sessionId,
        participantId,
        billId,
        moveOrders,
        guestType,
        guestCount,
        newBill,
      ),
    );

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
    updateGuestCounts,
    lockGuestCounts,
    mergeBill,
    moveGuest,
    endSession,
  };

  return (
    <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
  );
};
