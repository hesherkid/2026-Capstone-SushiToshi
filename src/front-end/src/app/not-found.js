"use client";
import Link from "next/link";

const NotFound = () => {
  return (
    <div style={{ textAlign: "center", padding: "50px" }}>
      <h2>Page Not Found</h2>
      <p>Sorry, the page you are looking for does not exist.</p>
      <Link href="/auth/login">Return to Login</Link>
    </div>
  );
};

export default NotFound;
