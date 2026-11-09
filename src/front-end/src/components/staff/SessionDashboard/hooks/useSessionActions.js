import { useState, useCallback, useMemo } from "react";
import api from "@/config/api";
import { createApiWrapper } from "@/utils/apiWrapper";
import { useNotification } from "@/contexts/NotificationContext";

/**
 * Safely converts value to integer with fallback.
 * @param {*} v - Value to convert
 * @param {number} d - Default value if conversion fails (default: 0)
 * @returns {number}
 */
const toInt = (v, d = 0) => (Number.isFinite(+v) ? parseInt(v, 10) : d);

// Validates that required IDs are present
const validateIds = (ids) => {
  for (const [name, value] of Object.entries(ids)) {
    if (!value) throw new Error(`Valid ${name} is required`);
  }
};

export const useSessionActions = (onSuccess) => {
  const [actionError, setActionError] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const { notifySuccess, notifyError } = useNotification();

  const clearActionError = useCallback(() => {
    setActionError(null);
  }, []);

  // Centralized error handler
  const handleError = useCallback(
    (error, context) => {
      const errorMessage = error.userMessage || `Failed to ${context}`;

      setActionError(errorMessage);
      notifyError(errorMessage);
    },
    [notifyError]
  );

  // Wrapper that handles all the repetitive loading/error logic
  const apiWrapper = useMemo(
    () =>
      createApiWrapper({
        setIsLoading,
        setActionError,
        handleError,
        onSuccess,
      }),
    [handleError, onSuccess]
  );

  // DINING SESSION OPERATION

  const createSession = useCallback(
    async (sessionData) => { 
      return apiWrapper(
        "createSession",
        async () => {
          const payload = {
            menu_id: toInt(sessionData?.menu_id),
            location_id: toInt(sessionData?.location_id),
            table_id: sessionData?.table_id
              ? toInt(sessionData?.table_id)
              : null,
            table_group_id: sessionData?.table_group_id
              ? toInt(sessionData?.table_group_id)
              : null,
            assignment_type: sessionData.assignment_type 
          };

          if (!payload.menu_id) throw new Error("Menu ID is required");
          if (!payload.location_id) throw new Error("Location ID is required");

          const response = await api.post(
            "/DiningSession/Create_Dinning_Session",
            payload
          );

          return response.data;
        },
        { triggerSuccess: true,
          successMessage: "Dining session created successfully"
        }
      );
    },
    [apiWrapper]
  );

  /**
   * List of dining sessions filtered by active status
   */
  const listDiningSessions = useCallback(
    async (activeOnly = true) => {
      return apiWrapper(
        "listDiningSessions",
        async () => {
          const res = await api.get("/DiningSession/get_list_dining_sessions", {
            params: { active_only: activeOnly },
          });
          return res.data || [];
        },
        { defaultReturn: [] }
      );
    },
    [apiWrapper]
  );

  /**
   * Get detailed information about a specific session
   */
  const getDiningSessionDetail = useCallback(
    async (sessionId) => {
      return apiWrapper("getDiningSessionDetail", async () => {
        const sid = toInt(sessionId);
        validateIds({ "session ID": sid });

        const res = await api.get(`/DiningSession/get_location/${sid}`);
        return res.data;
      });
    },
    [apiWrapper]
  );

  /**
   * Closes dining session after all bills are paid
   */
  const closeSession = useCallback(
    async (sessionId) => {
      return apiWrapper(
        "closeSession",
        async () => {
          const sid = toInt(sessionId);
          validateIds({ "session ID": sid });

          const res = await api.put(`/DiningSession/${sid}/close`);

          return res.data;
        },
        { triggerSuccess: true,
          successMessage: "Dining session closed successfully"
        }
      );
    },
    [apiWrapper]
  );

  /**
   * Add table to session
   */
  const addTable = useCallback(
    async (sessionId, tableId) => {
      return apiWrapper(
        "addTable",
        async () => {
          const sid = toInt(sessionId);
          const tid = toInt(tableId);
          validateIds({ "session ID": sid, "table ID": tid });

          const res = await api.post(`/DiningSession/${sid}/tables`, {
            table_id: tid,
          });

          return res.data;
        },
        { triggerSuccess: true,
          successMessage: "Table added to session successfully"
        }
      );
    },
    [apiWrapper]
  );

  /**
   * Add table group to a session
   */
  const addTableGroupToSession = useCallback(
    async (sessionId, tableGroupId) => {
      return apiWrapper(
        "addTableGroupToSession",
        async () => {
          const sid = toInt(sessionId);
          const tgid = toInt(tableGroupId);
          validateIds({ "session ID": sid, "table group ID": tgid });

          const res = await api.post(`/DiningSession/${sid}/table-groups`, {
            table_group_id: tgid,
          });

          return res.data;
        },
        { triggerSuccess: true,
          successMessage: "Table group added to session successfully"
        }
      );
    },
    [apiWrapper]
  );

  /**
   * Remove table from a session
   */
  const removeTableFromSession = useCallback(
    async (sessionId, tableId) => {
      return apiWrapper(
        "removeTableFromSession",
        async () => {
          const sid = toInt(sessionId);
          const tid = toInt(tableId);
          validateIds({ "session ID": sid, "table ID": tid });

          const res = await api.delete(`/DiningSession/${sid}/Tables/${tid}`);

          return res.data;
        },
        { triggerSuccess: true,
          successMessage: "Table removed from session successfully"
        }
      );
    },
    [apiWrapper]
  );

  /**
   * Remove table group from a session
   */
  const removeTableGroupFromSession = useCallback(
    async (sessionId, tableGroupId) => {
      return apiWrapper(
        "removeTableGroupFromSession",
        async () => {
          const sid = toInt(sessionId);
          const tgid = toInt(tableGroupId);
          validateIds({ "session ID": sid, "table group ID": tgid });

          const res = await api.delete(
            `/DiningSession/${sid}/table-groups/${tgid}`
          );

          return res.data;
        },
        { triggerSuccess: true,
          successMessage: "Table group removed from session successfully"
        }
      );
    },
    [apiWrapper]
  );

  /**
   * Get active session ID for current user
   */
  const getActiveSessionId = useCallback(
    async () => {
      return apiWrapper("getActiveSessionId", async () => {
        const res = await api.get(
          "/DiningSession/participants/active-session-id"
        );
        return res.data?.session_id || null;
      });
    },
    [apiWrapper]
  );

  /**
   * Get menu ID for a session
   */
  const getSessionMenuId = useCallback(
    async (sessionId) => {
      return apiWrapper("getSessionMenuId", async () => {
        const sid = toInt(sessionId);
        validateIds({ "session ID": sid });

        const res = await api.get(`/DiningSession/session-menu/${sid}`);
        return res.data?.menu_id || null;
      });
    },
    [apiWrapper]
  );

  // TABLE OPERATIONS

  /**
   * List empty tables
   */
  const listEmptyTables = useCallback(
    async (locationId) => {
      return apiWrapper(
        "listEmptyTables",
        async () => {
          const params = {};
          if (locationId) params.location_id = toInt(locationId);

          const res = await api.get("/TableEntity/empty", { params });
          return res.data || [];
        },
        { defaultReturn: [] }
      );
    },
    [apiWrapper]
  );

  // TABLE GROUP OPERATIONS

  /**
   * Fetches available table groups
   */
  const fetchAvailableTableGroups = useCallback(
    async (locationId) => {
      return apiWrapper(
        "fetchAvailableTableGroups",
        async () => {
          const params = {};
          if (locationId) params.location_id = toInt(locationId);

          const res = await api.get("/TableGroup/available", { params });
          return res.data || [];
        },
        { defaultReturn: [] }
      );
    },
    [apiWrapper]
  );

  // BILL OPERATIONS

  /**
   * Create a bill for a session
   */
  const createBill = useCallback(
    async (sessionId, billData) => {
      return apiWrapper(
        "createBill",
        async () => {
          const sid = toInt(sessionId);
          if (!sid) throw new Error("Session ID is required");

          const payload = {
            bill_name: billData.billName,
            adult_count: parseInt(billData.adultCount) || 0,
            child_count: parseInt(billData.childCount) || 0,
            senior_count: parseInt(billData.seniorCount) || 0,
            tot_count: parseInt(billData.totCount) || 0,
          };

          if (!payload.bill_name?.trim())
            throw new Error("Bill name is required");

          // Validate at least one guest
          const totalGuests =
            payload.adult_count +
            payload.child_count +
            payload.senior_count +
            payload.tot_count;

          if (totalGuests === 0) {
            throw new Error("At least one guest is required");
          }

          const res = await api.post(`/Bill/create_Bill/${sid}`, payload);

          return res.data;
        },
        { triggerSuccess: true,
          successMessage: "Bill created successfully"
        }
      );
    },
    [apiWrapper]
  );

  /**
   * Get all bills for a session
   */
  const getBills = useCallback(
    async (sessionId, tableId) => {
      return apiWrapper(
        "getBills",
        async () => {
          const sid = toInt(sessionId);
          if (!sid) throw new Error("Session ID is required");

          const params = {};
          if (tableId) params.table_id = toInt(tableId);

          const res = await api.get(`/Bill/get_bills/${sid}`, { params });
          return res.data || [];
        },
        { defaultReturn: [] }
      );
    },
    [apiWrapper]
  );

  /**
   * Get detailed bill summary with pricing breakdown
   */
  const getBillSummary = useCallback(
    async (sessionId, billId) => {
      return apiWrapper("getBillSummary", async () => {
        const sid = toInt(sessionId);
        const bid = toInt(billId);
        validateIds({ "session ID": sid, "bill ID": bid });

        const res = await api.get(`/Bill/summary/${bid}`, {
          params: { session_id: sid },
        });
        return res.data;
      });
    },
    [apiWrapper]
  );

  /**
   * Closes a bill
   */
  const closeBill = useCallback(
    async (sessionId, billId) => {
      return apiWrapper(
        "closeBill",
        async () => {
          const sid = toInt(sessionId);
          const bid = toInt(billId);
          validateIds({ "session ID": sid, "bill ID": bid });

          const res = await api.put(`/Bill/close_bill/${bid}`, null, {
            params: { session_id: sid },
          });

          return res.data;
        },
        { triggerSuccess: true,
          successMessage: "Bill closed successfully"
        }
      );
},
    [apiWrapper]
  );

  /**
   * Change an open bill's guest counts (clears its "guest entered" flags)
   */
  const updateGuestCounts = useCallback(
    async (sessionId, billId, counts) => {
      return apiWrapper(
        "updateGuestCounts",
        async () => {
          const sid = toInt(sessionId);
          const bid = toInt(billId);
          validateIds({ "session ID": sid, "bill ID": bid });

          const res = await api.put(
            `/Bill/guest_counts/${bid}`,
            {
              adult_count: toInt(counts.adultCount) || 0,
              senior_count: toInt(counts.seniorCount) || 0,
              child_count: toInt(counts.childCount) || 0,
              tot_count: toInt(counts.totCount) || 0,
            },
            { params: { session_id: sid } },
          );
          return res.data;
        },
        { triggerSuccess: true, successMessage: "Guest count updated" },
      );
    },
    [apiWrapper],
  );

  /**
   * Lock an open bill's guest counts so guests can't change them on their phones, or unlock them.
   * Locking clears its "guest entered" flags; staff can still change locked counts
   */
  const lockGuestCounts = useCallback(
    async (sessionId, billId, locked) => {
      return apiWrapper(
        "lockGuestCounts",
        async () => {
          const sid = toInt(sessionId);
          const bid = toInt(billId);
          validateIds({ "session ID": sid, "bill ID": bid });

          const res = await api.put(
            `/Bill/lock_guest_counts/${bid}`,
            { locked: Boolean(locked) },
            { params: { session_id: sid } },
          );
          return res.data;
        },
        { triggerSuccess: true, successMessage: locked ? "Guest count locked" : "Guest count unlocked" },
      );
    },
    [apiWrapper],
  );

  /**
   * Merge an open bill into another: its people, orders and guest counts move, then it's cancelled
   */
  const mergeBill = useCallback(
    async (sessionId, billId, intoBillId) => {
      return apiWrapper(
        "mergeBill",
        async () => {
          const sid = toInt(sessionId);
          const bid = toInt(billId);
          const into = toInt(intoBillId);
          validateIds({ "session ID": sid, "bill ID": bid, "bill to merge into": into });

          const res = await api.put(
            `/Bill/merge/${bid}`,
            { into_bill_id: into },
            { params: { session_id: sid } },
          );
          return res.data;
        },
        { triggerSuccess: true, successMessage: "Bills merged" },
      );
    },
    [apiWrapper],
  );

  /**
   * Move someone at the table onto another open bill, with or without their orders.
   * guestType ("adult", "senior", "child" or "toddler") also moves guestCount guests of that kind from
   * the old bill's count to the new one's; null leaves both counts alone.
   */
  const moveGuest = useCallback(
    async (sessionId, participantId, billId, moveOrders, guestType = null, guestCount = 1) => {
      return apiWrapper(
        "moveGuest",
        async () => {
          const sid = toInt(sessionId);
          const pid = toInt(participantId);
          const bid = toInt(billId);
          validateIds({ "session ID": sid, "participant ID": pid, "bill ID": bid });

          const res = await api.put(`/SessionParticipant/${sid}/participants/${pid}/bill`, {
            bill_id: bid,
            move_orders: moveOrders,
            guest_type: guestType,
            guest_count: guestCount,
          });
          return res.data;
        },
        { triggerSuccess: true, successMessage: "Guest moved" },
      );
    },
    [apiWrapper],
  );

  return {
    // State
    actionError,
    isLoading,

    // Dining session operations
    createDiningSession: createSession,
    listDiningSessions,
    getDiningSessionDetail,
    closeDiningSession: closeSession,
    addTableToSession: addTable,
    addTableGroupToSession,
    removeTableFromSession,
    removeTableGroupFromSession,
    getActiveSessionId,
    getSessionMenuId,

    // Table operations
    listEmptyTables,

    // Table group operations
    fetchAvailableTableGroups,

    // Bill operations
    createBill,
    getBills,
    getBillSummary,
    closeBill,
    updateGuestCounts,
    lockGuestCounts,
    mergeBill,
    moveGuest,

    // Utilities
    clearActionError,
  };
};
