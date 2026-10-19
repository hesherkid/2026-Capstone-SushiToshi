
import api from "@/config/api";
import { decodeToken, getUserRoles } from "@/config/auth";

let dispatchAuthChange;

if (typeof window !== "undefined") {
  import("@/hooks/useAuth").then((module) => {
    dispatchAuthChange = module.dispatchAuthChange;
  });
}

const notifyAuthChange = () => {
  if (typeof window !== "undefined") {
    dispatchAuthChange?.();
  }
};

// --------------------------------------------------
// TOKEN MANAGEMENT
// --------------------------------------------------

export const getAuthToken = () => {
  if (typeof window === "undefined") return null;

  return localStorage.getItem("authToken");
};

// --------------------------------------------------
// LOGIN
// --------------------------------------------------

// Saves a login response's token (and expiry/user if present) for the api client
const storeSession = (data) => {
  if (!data.access_token || typeof window === "undefined") return;

  localStorage.setItem("authToken", data.access_token);
  localStorage.setItem("access_token", data.access_token);

  if (data.expires_in) {
    const expiresAt = Date.now() + data.expires_in * 1000;

    localStorage.setItem(
      "tokenExpiresAt",
      expiresAt.toString()
    );
  }

  if (data.user) {
    localStorage.setItem("user", JSON.stringify(data.user));
  }

  notifyAuthChange();
};

export const loginUser = async (email, password) => {
  const response = await api.post("/auth/login", {
    email,
    password,
  });

  storeSession(response.data);
  return response.data;
};

// A guest token must have this long left to be reused; with less, the visit could outlast it
export const GUEST_REUSE_MARGIN_MS = 15 * 60 * 1000;

const GUEST_EMAIL_DOMAIN = "@guest.sushitoshi.invalid";
const EMAIL_CLAIM = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

// The saved token, if it belongs to a guest and has time left. "Continue as Guest" reuses it so
// a phone that rescans the QR code (or refreshes the login page) stays the same guest instead
// of adding another participant to the table. Tokens from before the "guest" claim was added
// are recognised by their email domain.
export const getReusableGuestToken = () => {
  if (typeof window === "undefined") return null;

  const token = localStorage.getItem("authToken");
  const claims = token ? decodeToken(token) : null;
  if (!claims) return null;

  const email = claims[EMAIL_CLAIM] ?? claims.email ?? "";
  const isGuest = claims.guest === "true" || email.toLowerCase().endsWith(GUEST_EMAIL_DOMAIN);
  const hasTimeLeft = claims.exp * 1000 > Date.now() + GUEST_REUSE_MARGIN_MS;

  return isGuest && hasTimeLeft ? token : null;
};

// "Continue as Guest": the server creates a throwaway account for this guest
// ("Guest K7P2"), so each guest at a table is their own participant
export const loginAsGuest = async () => {
  const response = await api.post("/auth/guest");

  storeSession(response.data);
  return response.data;
};

// --------------------------------------------------
// REGISTER
// --------------------------------------------------

export const registerUser = async ({
  email,
  password,
  firstName,
  lastName,
}) => {
  const response = await api.post("/auth/register", {
    email,
    password,
    first_name: firstName,
    last_name: lastName,
  });

  return response.data;
};

// --------------------------------------------------
// EMAIL VERIFICATION
// --------------------------------------------------

export const verifyEmail = async (token) => {
  const response = await api.post("/auth/verify-email", {
    token,
  });

  return response.data;
};

// --------------------------------------------------
// RESEND VERIFICATION
// --------------------------------------------------

export const resendVerificationEmail = async (email) => {
  const response = await api.post(
    "/auth/resend-verification",
    { email }
  );

  return response.data;
};

// --------------------------------------------------
// FORGOT PASSWORD
// --------------------------------------------------

export const forgotPassword = async (email) => {
  const response = await api.post(
    "/auth/forgot-password",
    { email }
  );

  return response.data;
};

// --------------------------------------------------
// RESET PASSWORD
// --------------------------------------------------

export const resetPassword = async (token, newPassword) => {
  const response = await api.post(
    "/auth/reset-password",
    {
      token,
      newPassword,
    }
  );

  return response.data;
};

// --------------------------------------------------
// LOGOUT
// --------------------------------------------------

export const logoutUser = () => {
  if (typeof window === "undefined") return;

  localStorage.removeItem("authToken");
  localStorage.removeItem("access_token");
  localStorage.removeItem("tokenExpiresAt");
  localStorage.removeItem("user");

  //notifyAuthChange();
  window.location.href = "/auth/login";
};

// --------------------------------------------------
// CURRENT USER
// --------------------------------------------------

export const getCurrentUser = async () => {
  const response = await api.get("/auth/me");
  return response.data;
};

// --------------------------------------------------
// AUTHENTICATION STATUS
// --------------------------------------------------

export const isAuthenticated = () => {
  if (typeof window === "undefined") return false;

  const token = getAuthToken();
  if (!token) return false;

  try {
    const claims = decodeToken(token);

    if (!claims?.exp) return false;

    return claims.exp * 1000 > Date.now();
  } catch {
    return false;
  }
};

// --------------------------------------------------
// USER ROLES
// --------------------------------------------------

export const getCurrentUserRoles = () => {
  const token = getAuthToken();
  return getUserRoles(token);
};
