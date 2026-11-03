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
            Menu_Id: toInt(sessionData?.Menu_Id),
            Location_Id: toInt(sessionData?.Location_Id),
            Table_Id: sessionData?.Table_Id
              ? toInt(sessionData?.Table_Id)
              : null,
            TableGroup_Id: sessionData?.TableGroup_Id
              ? toInt(sessionData?.TableGroup_Id)
              : null,
            AssignmentType: sessionData.AssignmentType 
          };

          if (!payload.Menu_Id) throw new Error("Menu ID is required");
          if (!payload.Location_Id) throw new Error("Location ID is required");

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
            params: { ActiveOnly: activeOnly },
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
            table_Id: tid,
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
            tableGroup_Id: tgid,
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
        return res.data?.Session_Id || res.data?.session_id || null;
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
        return res.data?.Menu_Id || res.data?.menu_id || null;
      });
    },
    [apiWrapper]
  );

  // SESSION OPERATIONS

  /**
   * Fetches all active sessions
   */
  const fetchActiveSessions = useCallback(
    async () => {
      return apiWrapper(
        "fetchActiveSessions",
        async () => {
          const res = await api.get("/session/active");
          return res.data || [];
        },
        { defaultReturn: [] }
      );
    },
    [apiWrapper]
  );

  /**
   * Fetch a session by ID
   */
  const fetchSessionById = useCallback(
    async (sessionId) => {
      return apiWrapper("fetchSessionById", async () => {
        const sid = toInt(sessionId);
        validateIds({ "session ID": sid });

        const res = await api.get(`/session/${sid}`);
        return res.data;
      });
    },
    [apiWrapper]
  );

  /**
   * Get session for a table
   */

  const fetchSessionByTable = useCallback(
    async (tableId) => {
      return apiWrapper("fetchSessionByTable", async () => {
        const tid = toInt(tableId);
        validateIds({ "table ID": tid });

        const res = await api.get(`/session/table/${tid}`);
        return res.data;
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
          if (locationId) params.locationId = toInt(locationId);

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
          if (locationId) params.locationId = toInt(locationId);

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
            Bill_Name: billData.billName || billData.Bill_Name,
            Adult_Count:
              parseInt(billData.adultCount || billData.Adult_Count) || 0,
            Child_Count:
              parseInt(billData.childCount || billData.Child_Count) || 0,
            Senior_Count:
              parseInt(billData.seniorCount || billData.Senior_Count) || 0,
            Tot_Count: parseInt(billData.totCount || billData.Tot_Count) || 0,
          };

          if (!payload.Bill_Name?.trim())
            throw new Error("Bill name is required");

          // Validate at least one guest
          const totalGuests =
            payload.Adult_Count +
            payload.Child_Count +
            payload.Senior_Count +
            payload.Tot_Count;

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
          if (tableId) params._table_id = toInt(tableId);

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
          params: { _session_id: sid },
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
            params: { _session_id: sid },
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
            { params: { _session_id: sid } },
          );
          return res.data;
        },
        { triggerSuccess: true, successMessage: "Guest count updated" },
      );
    },
    [apiWrapper],
  );

  /**
   * Move someone at the table onto another open bill, with or without their orders
   */
  const moveGuest = useCallback(
    async (sessionId, participantId, billId, moveOrders) => {
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

    // Session operations
    fetchActiveSessions,
    fetchSessionById,
    fetchSessionByTable,

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
    moveGuest,

    // Utilities
    clearActionError,
  };
};
