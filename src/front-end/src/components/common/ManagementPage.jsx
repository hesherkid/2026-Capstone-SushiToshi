import React, { useEffect, useRef } from "react";
import { Box, Alert } from "@mui/material";
import ManagementHeader from "@/components/common/ManagementHeader";
import ManagementGrid from "@/components/common/ManagementGrid";

const SUCCESS_AUTO_HIDE_MS = 6000;

// Standard admin management layout: header, status alerts, grid, then any dialogs as children
export const ManagementPage = ({
  title,
  addLabel,
  onAdd,
  headerActions = null,
  error = null,
  onErrorClose,
  success = null,
  onSuccessClose,
  gridProps,
  children,
  sx = {},
}) => {
  // Success messages dismiss themselves; errors stay until closed.
  // Ref keeps an inline onSuccessClose from restarting the timer every render.
  const onSuccessCloseRef = useRef(onSuccessClose);
  onSuccessCloseRef.current = onSuccessClose;

  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(
      () => onSuccessCloseRef.current?.(),
      SUCCESS_AUTO_HIDE_MS
    );
    return () => clearTimeout(timer);
  }, [success]);

  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", p: 3, ...sx }}>
      <ManagementHeader
        title={title}
        addLabel={addLabel}
        onAdd={onAdd}
        actions={headerActions}
      />

      {error && (
        <Alert severity="error" onClose={onErrorClose} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {success && (
        <Alert severity="success" onClose={onSuccessClose} sx={{ mb: 2 }}>
          {success}
        </Alert>
      )}

      <ManagementGrid {...gridProps} />

      {children}
    </Box>
  );
};

export default ManagementPage;
