"use client";
import RequireRole from "@/components/auth/RequireRole";

const ROLES = ["admin", "staff"];

// Staff and admins only
export default function StaffLayout({ children }) {
  return <RequireRole roles={ROLES}>{children}</RequireRole>;
}
