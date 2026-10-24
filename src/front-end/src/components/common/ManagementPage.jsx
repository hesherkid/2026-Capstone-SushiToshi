import React, { useEffect, useRef } from "react";
import { Box, Alert, IconButton } from "@mui/material";
import { X } from "lucide-react";
import ManagementHeader from "@/components/common/ManagementHeader";
import ManagementGrid from "@/components/common/ManagementGrid";

const SUCCESS_AUTO_HIDE_MS = 6000;

export const ManagementPage = ({
  title,
  addLabel,
  onAdd,
  headerActions = null,
  error = null,
  onErrorClose,
  success = null,
  onSuccessClose,
  successAction = null,
  successAutoHideMs = SUCCESS_AUTO_HIDE_MS,
  gridProps,
  children,
  sx = {},
}) => {
  // Success messages dismiss themselves; errors stay until closed.
  const onSuccessCloseRef = useRef(onSuccessClose);
  onSuccessCloseRef.current = onSuccessClose;

  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(
      () => onSuccessCloseRef.current?.(),
      successAutoHideMs,
    );
    return () => clearTimeout(timer);
  }, [success, successAutoHideMs]);

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
        <Alert
          severity="success"
          onClose={onSuccessClose}
          action={
            successAction ? (
              <>
                {successAction}
                <IconButton
                  aria-label="Close"
                  color="inherit"
                  size="small"
                  onClick={onSuccessClose}
                >
                  <X size={18} />
                </IconButton>
              </>
            ) : undefined
          }
          sx={{ mb: 2 }}
        >
          {success}
        </Alert>
      )}

      <ManagementGrid {...gridProps} />

      {children}
    </Box>
  );
};

export default ManagementPage;
