"use client";

import { Suspense } from "react";
import QrLoginRedirect from "@/components/auth/QrLoginRedirect";

// Legacy QR link: /start-session?tableNumber=4&locationId=1
export default function StartSessionPage() {
  return (
    <Suspense fallback={null}>
      <QrLoginRedirect tableParam="tableNumber" locationParam="locationId" />
    </Suspense>
  );
}
