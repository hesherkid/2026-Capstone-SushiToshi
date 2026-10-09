import axios from "axios";

export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ??
  (typeof window !== "undefined" ? "" : "http://127.0.0.1:5264");

export const api = axios.create({
  baseURL: `${API_BASE_URL}/api`,
  timeout: 1000000,
  headers: {
    "Content-Type": "application/json",
  },
});

// Simple token-based auth interceptor
api.interceptors.request.use(
  (config) => {
    // Skip auth for guest users or login endpoint
    if (config.url?.includes("/auth/login")) {
      return config;
    }

    // Add token from localStorage if it exists
    const token =
      typeof window !== "undefined" ? localStorage.getItem("authToken") : null;

    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  },
);

// Optional: Handle 401 responses (token expired)
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (
      error.response?.status === 401 &&
      !error.config?.url?.includes("/auth/login")
    ) {
      // Clear token and redirect to login
      if (typeof window !== "undefined") {
        localStorage.removeItem("authToken");
        window.location.href = "/auth/login";
      }
    }
    return Promise.reject(error);
  },
);

export default api;
