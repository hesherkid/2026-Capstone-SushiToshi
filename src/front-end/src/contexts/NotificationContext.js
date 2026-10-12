import React, { createContext, useState, useContext, useCallback, useMemo } from "react";

export const NotificationContext = createContext(null);

export const useNotification = () => {
  const context = useContext(NotificationContext);
  if (!context) {
    throw new Error("useNotification must be used within NotificationProvider");
  }

  // Depend on the stable showNotification, not the whole context, so these
  // helpers keep the same identity across notification state changes
  const { showNotification } = context;

  const notifySuccess = useCallback(
    (message, duration = 6000) => {
      showNotification(message, "success", duration);
    },
    [showNotification]
  );

  const notifyError = useCallback(
    (message, duration = 6000) => {
      showNotification(message, "error", duration);
    },
    [showNotification]
  );

  const notifyWarning = useCallback(
    (message, duration = 6000) => {
      showNotification(message, "warning", duration);
    },
    [showNotification]
  );

  const notifyInfo = useCallback(
    (message, duration = 6000) => {
      showNotification(message, "info", duration);
    },
    [showNotification]
  );

  return {
    ...context,
    notifySuccess,
    notifyError,
    notifyWarning,
    notifyInfo,
  };
};

export const NotificationProvider = ({ children }) => {
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState("");
  const [severity, setSeverity] = useState("info");
  const [autoHideDuration, setAutoHideDuration] = useState(6000);

  const showNotification = useCallback(
    (message, severity = "info", duration = 6000) => {
      setMessage(message);
      setSeverity(severity);
      setAutoHideDuration(duration);
      setOpen(true);
    },
    []
  );

  const hideNotification = useCallback(() => {
    setOpen(false);
  }, []);

  const value = useMemo(
    () => ({
      open,
      message,
      severity,
      autoHideDuration,
      showNotification,
      hideNotification,
    }),
    [open, message, severity, autoHideDuration, showNotification, hideNotification]
  );

  return (
    <NotificationContext.Provider value={value}>
      {children}
    </NotificationContext.Provider>
  );
};