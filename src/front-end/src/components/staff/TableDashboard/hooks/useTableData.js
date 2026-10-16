import { useState, useCallback, useMemo } from "react";
import api from "@/config/api";
import storage from "@/utils/storage";

const NO_LOCATION_ERROR =
  "Invalid location. Make sure you have selected a location in the navigation bar dropdown at the top of the page.";

export const TABLE_STATUS = {
  AVAILABLE: "available",
  IN_SESSION: "inSession",
  INACTIVE: "inactive",
};

// /TableEntity/empty lists active tables with no open session (directly or via their group),
// so an active table missing from it is in session.
const getTableStatus = (table, emptyIds) => {
  if (!table.is_active) return TABLE_STATUS.INACTIVE;
  return emptyIds.has(table.table_Id)
    ? TABLE_STATUS.AVAILABLE
    : TABLE_STATUS.IN_SESSION;
};

export const useTableData = () => {
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [rawTables, setRawTables] = useState([]);
  const [rawGroups, setRawGroups] = useState([]);
  const [emptyIds, setEmptyIds] = useState(new Set());

  const fetchAllData = useCallback(async () => {
    const locationId = storage.get("branch-location");
    if (!locationId) {
      setError(NO_LOCATION_ERROR);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);
    try {
      const [tablesRes, groupsRes, emptyRes] = await Promise.all([
        api.get(`/TableEntity?locationId=${locationId}`),
        api.get(`/TableGroup?locationId=${locationId}`),
        api.get(`/TableEntity/empty?locationId=${locationId}`),
      ]);
      setRawTables(tablesRes.data || []);
      setRawGroups(groupsRes.data || []);
      setEmptyIds(new Set((emptyRes.data || []).map((t) => t.table_Id)));
    } catch (err) {
      console.error("Error fetching table data:", err);
      setError("Failed to load table data");
    } finally {
      setIsLoading(false);
    }
  }, []);

  const tables = useMemo(() => {
    const groupNames = new Map(
      rawGroups.map((g) => [g.tableGroup_Id, g.group_Name])
    );
    return rawTables.map((table) => ({
      ...table,
      status: getTableStatus(table, emptyIds),
      group_Name: groupNames.get(table.tableGroup_Id) ?? null,
    }));
  }, [rawTables, rawGroups, emptyIds]);

  const tableGroups = useMemo(
    () =>
      rawGroups.map((group) => {
        const groupTables = group.tables || [];
        const inSession = groupTables.some(
          (t) => getTableStatus(t, emptyIds) === TABLE_STATUS.IN_SESSION
        );
        let status = TABLE_STATUS.AVAILABLE;
        if (!group.is_Active) status = TABLE_STATUS.INACTIVE;
        else if (inSession) status = TABLE_STATUS.IN_SESSION;

        return {
          ...group,
          status,
          total_seats: groupTables.reduce((sum, t) => sum + t.seat_count, 0),
        };
      }),
    [rawGroups, emptyIds]
  );

  const tableSummary = useMemo(
    () => ({
      total: tables.length,
      available: tables.filter((t) => t.status === TABLE_STATUS.AVAILABLE)
        .length,
      inSession: tables.filter((t) => t.status === TABLE_STATUS.IN_SESSION)
        .length,
      inactive: tables.filter((t) => t.status === TABLE_STATUS.INACTIVE).length,
    }),
    [tables]
  );

  return {
    isLoading,
    error,
    setError,
    tables,
    tableGroups,
    tableSummary,
    fetchAllData,
  };
};
