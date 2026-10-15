import React from "react";
import NextLink from "next/link";
import {
  Alert,
  AlertTitle,
  Box,
  Button,
  Chip,
  List,
  ListItem,
  ListItemText,
  Typography,
} from "@mui/material";
import { keyframes } from "@mui/material/styles";
import { Bell, CheckCircle2 } from "lucide-react";
import { formatTime } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import { MoreLink, PREVIEW_LIMIT } from "@/components/home/MoreLink";

const ring = keyframes`
  0%, 100% { transform: rotate(0); }
  20% { transform: rotate(15deg); }
  40% { transform: rotate(-15deg); }
  60% { transform: rotate(10deg); }
  80% { transform: rotate(-10deg); }
`;

// Pending table requests ("call server") for the staff/admin home page
export const ServiceRequestAlerts = ({ requests = [], newRequestIds = new Set() }) => {
  if (requests.length === 0) {
    return (
      <Alert
        severity="success"
        icon={<CheckCircle2 size={22} />}
        sx={{ alignItems: "center" }}
      >
        No tables are waiting for help right now.
      </Alert>
    );
  }

  const hasNew = requests.some((r) => newRequestIds.has(r.request_id));
  const preview = requests.slice(0, PREVIEW_LIMIT);

  return (
    <Alert
      severity="warning"
      role="alert"
      icon={
        <Box
          component="span"
          sx={{ display: "flex", animation: hasNew ? `${ring} 1s ease-in-out 2` : "none" }}
        >
          <Bell size={22} />
        </Box>
      }
      action={
        <Button
          component={NextLink}
          href="/dashboard/sessions"
          color="inherit"
          size="small"
          sx={{ whiteSpace: "nowrap" }}
        >
          Open Sessions
        </Button>
      }
      sx={{ "& .MuiAlert-message": { width: "100%" } }}
    >
      <AlertTitle sx={{ fontWeight: 700 }}>
        {requests.length === 1
          ? "1 table needs help"
          : `${requests.length} tables need help`}
      </AlertTitle>
      <List dense disablePadding>
        {preview.map((request) => (
          <ListItem key={request.request_id} disableGutters sx={{ gap: 1 }}>
            <ListItemText
              primary={
                <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                  <Typography component="span" sx={{ fontWeight: 600 }}>
                    Table {request.table_number || "?"}
                  </Typography>
                  {newRequestIds.has(request.request_id) && (
                    <Chip label="New" size="small" color="warning" />
                  )}
                  <Typography component="span" variant="body2" color="text.secondary">
                    {formatTime(request.created_at)}
                  </Typography>
                </Box>
              }
              secondary={request.notes || "Requested a server"}
            />
          </ListItem>
        ))}
      </List>
      <MoreLink
        hiddenCount={requests.length - preview.length}
        href="/dashboard/sessions"
        noun={requests.length - preview.length === 1 ? "request" : "requests"}
      />
    </Alert>
  );
};

export default ServiceRequestAlerts;
