"use client";

import { Suspense } from "react";
import QrLoginRedirect from "@/components/auth/QrLoginRedirect";

// Legacy QR link: /join?table=4&location=1
export default function JoinPage() {
  return (
    <Suspense fallback={null}>
      <QrLoginRedirect tableParam="table" locationParam="location" />
    </Suspense>
  );
}
