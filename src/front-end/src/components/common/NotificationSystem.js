import React from "react";
import { Snackbar, Alert, Slide } from "@mui/material";
import { useNotification } from "@/contexts/NotificationContext";

function SlideTransition(props) {
  return <Slide {...props} direction="up" />;
}

const NotificationSystem = () => {
  const { open, message, severity, autoHideDuration, hideNotification } =
    useNotification();

  const handleClose = (event, reason) => {
    if (reason === "clickaway") {
      return;
    }
    hideNotification();
  };

  return (
    <Snackbar
      open={open}
      autoHideDuration={autoHideDuration}
      onClose={handleClose}
      anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      slots={{ transition: SlideTransition }}
      sx={{
        bottom: { xs: 70, sm: 24 },
        pointerEvents: "none",
        "& .MuiAlert-root": { pointerEvents: "auto" },
      }}
    >
      <Alert
        onClose={handleClose}
        severity={severity}
        variant="filled"
        sx={{
          width: "100%",
          maxWidth: { xs: "calc(100vw - 32px)", sm: "400px" },
        }}
      >
        {message}
      </Alert>
    </Snackbar>
  );
};

export default NotificationSystem;
