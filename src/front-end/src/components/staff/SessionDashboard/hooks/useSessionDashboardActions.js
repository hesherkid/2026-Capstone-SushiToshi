import api from "@/config/api";
import { useNotification } from "@/contexts/NotificationContext";
import { useSession } from "../context/SessionContext";

// Row and detail-dialog actions for the session dashboard
export const useSessionDashboardActions = () => {
  const { openDialog, openSession, endSession, refreshData } = useSession();
  const { notifySuccess, notifyError } = useNotification();

  return {
    newSession: () => openDialog("newSession"),

    viewSession: (session) => openSession(session.session_Id),

    newBill: (session) => openDialog("newBill", session.session_Id),

    // Staff ordering on one of the table's open bills
    addOrder: (session, bill) =>
      openDialog("staffOrder", { sessionId: session.session_Id, billId: bill.bill_Id }),

    // Fixing a bill's guest counts
    editGuests: (session, bill) =>
      openDialog("editGuests", { sessionId: session.session_Id, bill }),

    // Moving someone at the table onto another bill
    moveGuest: (session, person) =>
      openDialog("moveGuest", { sessionId: session.session_Id, person }),

    endSession: (session) =>
      openDialog("confirm", {
        title: "End session",
        message: `End Session #${session.session_Id} for ${
          session.table_Numbers?.length > 1 ? "tables" : "table"
        } ${session.table_Numbers?.join(", ") || "—"}?`,
        confirmLabel: "End Session",
        onConfirm: () => endSession(session.session_Id),
      }),

    completeRequest: async (request) => {
      try {
        await api.post(`/ServiceRequest/${request.request_id}/complete`);
        notifySuccess("Service request completed");
        await refreshData();
      } catch (error) {
        console.error("Error completing request:", error);
        notifyError("Failed to complete service request");
      }
    },
  };
};
