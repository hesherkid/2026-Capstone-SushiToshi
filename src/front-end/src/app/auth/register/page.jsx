import { Suspense } from "react";
import RegisterForm from "@/components/auth/RegisterForm";

export const metadata = {
  title: "Register | Sushi Toshi",
  description: "Create a new account at Sushi Toshi",
};

export default function RegisterPage() {
  return (
    <Suspense fallback={<div>Loading...</div>}>
      <RegisterForm />
    </Suspense>
  );
}
