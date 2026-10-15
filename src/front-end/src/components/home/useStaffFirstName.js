"use client";

import { useEffect, useState } from "react";
import api from "@/config/api";
import { decodeToken } from "@/config/auth";
import { getAuthToken } from "@/utils/auth";

const NAME_ID_CLAIM =
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";

const nameFromEmail = (email) => {
  const local = (email || "").split("@")[0];
  return local ? local.charAt(0).toUpperCase() + local.slice(1) : "";
};

// First name for the signed-in staff member or admin. The token only carries. Falls back to the email.

export default function useStaffFirstName(userEmail) {
  const [firstName, setFirstName] = useState(() => nameFromEmail(userEmail));

  useEffect(() => {
    setFirstName((current) => current || nameFromEmail(userEmail));

    const token = getAuthToken();
    const claims = token ? decodeToken(token) : null;
    const userId = claims?.[NAME_ID_CLAIM] ?? claims?.nameid;
    if (!userId) return;

    let cancelled = false;
    api
      .get(`/staff/${userId}`)
      .then(({ data }) => {
        if (!cancelled && data?.first_name) setFirstName(data.first_name);
      })
      .catch(() => {});

    return () => {
      cancelled = true;
    };
  }, [userEmail]);

  return firstName;
}
