import { Suspense } from "react";
import ResendVerificationForm from "@/components/auth/ResendVerificationForm";

export const metadata = {
  title: "Resend Verification Email",
  description: "Get a new Sushi Toshi email verification link",
};

export default function ResendVerificationPage() {
  return (
    <Suspense fallback={null}>
      <ResendVerificationForm />
    </Suspense>
  );
}
