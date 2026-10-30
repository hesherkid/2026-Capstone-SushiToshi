import { useState, useEffect } from "react";
import {
  decodeToken,
  getUserRoles,
  isTokenExpired,
} from "@/config/auth";
import { getAuthToken } from "@/utils/auth";
import storage from "@/utils/storage";

// Custom event for auth changes
export const AUTH_EVENT = "auth-change";

// Helper to dispatch auth change event
export const dispatchAuthChange = () => {
  if (typeof window !== 'undefined') {
    window.dispatchEvent(new Event(AUTH_EVENT));
  }
};

// Helper function to clear auth tokens
export const clearAuthTokens = () => {
  if (typeof window === 'undefined') return;
  
  storage.remove("authToken");
  storage.remove("tokenExpiresAt");
  storage.remove("user");
  storage.remove("guest");
  
  dispatchAuthChange();
};

export const useAuth = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [userRole, setUserRole] = useState(null);
  const [userRoles, setUserRoles] = useState([]);
  const [userId, setUserId] = useState(null);
  const [userEmail, setUserEmail] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const checkAuth = () => {
      const token = getAuthToken();

      if (!token) {
        setIsAuthenticated(false);
        setUserRole(null);
        setUserRoles([]);
        setUserId(null);
        setUserEmail(null);
        setLoading(false);
        return;
      }

      // Check if token is expired
      if (isTokenExpired(token)) {
        clearAuthTokens();
        setIsAuthenticated(false);
        setUserRole(null);
        setUserRoles([]);
        setUserId(null);
        setUserEmail(null);
        setLoading(false);
        return;
      }

      // Decode and set user info
      try {
        const claims = decodeToken(token);
        
        if (!claims) {
          throw new Error("Failed to decode token");
        }

        // Extract user information from token claims
        const roles = getUserRoles(token).map(role => role.toLowerCase());;
        const primaryRole = roles[0] || null; // Get first role as primary
        
        const id = claims.sub || 
                   claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
                   claims.userId || 
                   claims.id || 
                   null;
        const email = claims.email || 
                      claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
                      claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ||
                      null;

        setUserRoles(roles);
        setUserRole(primaryRole);
        setUserId(id);
        setUserEmail(email);
        setIsAuthenticated(true);
      } catch (error) {
        console.error("Error decoding token:", error);
        clearAuthTokens();
        setIsAuthenticated(false);
        setUserRole(null);
        setUserRoles([]);
        setUserId(null);
        setUserEmail(null);
      } finally {
        setLoading(false);
      }
    };

    checkAuth();

    // Listen for storage changes (e.g., login/logout in other tabs)
    const handleStorageChange = (event) => {
      if (event.key === "authToken") {
        checkAuth();
      }
    };
    window.addEventListener("storage", handleStorageChange);

    // Same-tab updates
    const handleAuthChange = () => {
      checkAuth();
    };
    window.addEventListener(AUTH_EVENT, handleAuthChange);

    return () => {
      window.removeEventListener("storage", handleStorageChange);
      window.removeEventListener(AUTH_EVENT, handleAuthChange);
    };
  }, []);

  // Check if user has a specific role
  const hasRole = (role) => {
    return userRoles.includes(role);
  };

  // Check if user has any of the specified roles
  const hasAnyRole = (roles) => {
    return roles.some(role => userRoles.includes(role));
  };

  // Check if user has all of the specified roles
  const hasAllRoles = (roles) => {
    return roles.every(role => userRoles.includes(role));
  };

  return {
    isAuthenticated,
    userRole, // Primary role (first role)
    userRoles, // All roles array
    userId,
    userEmail,
    loading,
    hasRole,
    hasAnyRole,
    hasAllRoles,
  };
};

export default useAuth;