"use client";
import RequireRole from "@/components/auth/RequireRole";

const ROLES = ["admin"];

// Admin only, matching the API's admin controllers
export default function AdminLayout({ children }) {
  return <RequireRole roles={ROLES}>{children}</RequireRole>;
}
