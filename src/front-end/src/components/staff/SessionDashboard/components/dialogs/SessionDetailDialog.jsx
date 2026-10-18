import { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Box,
  Typography,
  Button,
  IconButton,
  Chip,
  List,
  ListItem,
  ListItemText,
  Divider,
  Tooltip,
} from "@mui/material";
import { X, Check, Receipt, ReceiptText, LogOut, Bell } from "lucide-react";
import { useSession } from "../../context/SessionContext";
import { useSessionDashboardActions } from "../../hooks/useSessionDashboardActions";
import {
  formatTime,
  formatBillStatus,
  getBillStatusColor,
  formatGuestBreakdown,
  sortBillsByStatus,
} from "../../utils/sessionHelpers";
import { tableLabel, isOpenBill, SeatedFor } from "../SessionColumns";
import BillSummaryDialog from "./BillSummaryDialog";
import ConfirmDialog from "./ConfirmDialog";

const SectionHeading = ({ icon: Icon, children }) => (
  <Typography
    variant="subtitle2"
    component="h3"
    sx={{ display: "flex", alignItems: "center", gap: 1, px: 3, pt: 2, pb: 1 }}
  >
    <Icon size={16} aria-hidden />
    {children}
  </Typography>
);

const SessionDetailDialog = () => {
  const { selectedSession: session, closeSession, requestsBySession } =
    useSession();
  const actions = useSessionDashboardActions();
  const [summaryBillId, setSummaryBillId] = useState(null);
  const [billToClose, setBillToClose] = useState(null);

  const requests = session ? requestsBySession[session.session_Id] || [] : [];
  const bills = sortBillsByStatus(session?.bills || []);
  const hasOpenBills = bills.some(isOpenBill);


  return (
    <>
      <Dialog open={!!session} onClose={closeSession} maxWidth="sm" fullWidth>
        {session && (
          <>
            <DialogTitle>
              <Box
                sx={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "flex-start",
                }}
              >
                <Box>
                  <Typography variant="h6" component="span">
                    Dining Session #{session.session_Id} ·{" "}
                    {tableLabel(session.table_Numbers)}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Started {formatTime(session.started_At)} ·{" "}
                    <SeatedFor startedAt={session.started_At} /> ·{" "}
                    {session.active_Participants ?? 0} guest
                    {session.active_Participants === 1 ? "" : "s"}
                  </Typography>
                </Box>
                <IconButton aria-label="Close" onClick={closeSession} size="small">
                  <X size={20} />
                </IconButton>
              </Box>
            </DialogTitle>

            <DialogContent dividers sx={{ px: 0, py: 0 }}>
              {requests.length > 0 && (
                <>
                  <SectionHeading icon={Bell}>
                    Service requests ({requests.length})
                  </SectionHeading>
                  <List dense sx={{ py: 0 }}>
                    {requests.map((request) => (
                      <ListItem
                        key={request.request_id}
                        sx={{ px: 3, bgcolor: "warning.light", mb: 0.5 }}
                        secondaryAction={
                          <Tooltip title="Mark done">
                            <IconButton
                              edge="end"
                              aria-label="Mark request done"
                              color="success"
                              onClick={() => actions.completeRequest(request)}
                            >
                              <Check size={18} />
                            </IconButton>
                          </Tooltip>
                        }
                      >
                        <ListItemText
                          primary={request.notes || "Requested a server"}
                          secondary={`Table ${request.table_number ?? "?"} · ${formatTime(
                            request.created_at
                          )}`}
                        />
                      </ListItem>
                    ))}
                  </List>
                  <Divider />
                </>
              )}

              <SectionHeading icon={Receipt}>
                Bills ({bills.length})
              </SectionHeading>
              {bills.length === 0 ? (
                <Typography color="text.secondary" sx={{ px: 3, pb: 2 }}>
                  No bills yet.
                </Typography>
              ) : (
                <List sx={{ py: 0 }}>
                  {bills.map((bill, index) => (
                    <Box key={bill.bill_Id}>
                      <ListItem
                        sx={{ px: 3, gap: 1 }}
                        secondaryAction={
                          <Box sx={{ display: "flex", gap: 1 }}>
                            <Tooltip title="View bill summary">
                              <Button
                                aria-label={`View ${bill.bill_Name} summary`}
                                onClick={() => setSummaryBillId(bill.bill_Id)}
                              >
                                <Receipt />
                                View Bill Summary
                              </Button>
                            </Tooltip>
                            {isOpenBill(bill) && (
                              <Button
                                size="small"
                                variant="outlined"
                                aria-label={`Close bill ${bill.bill_Name}`}
                                onClick={() => setBillToClose(bill)}
                              >
                                Close Bill
                              </Button>
                            )}
                          </Box>
                        }
                      >
                        <ListItemText
                          primary={
                            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                              {bill.bill_Name}
                              <Chip
                                label={formatBillStatus(bill.status)}
                                color={getBillStatusColor(bill.status)}
                                size="small"
                              />
                            </Box>
                          }
                          secondary={formatGuestBreakdown(bill)}
                          sx={{ pr: 16 }}
                        />
                      </ListItem>
                      {index < bills.length - 1 && <Divider component="li" />}
                    </Box>
                  ))}
                </List>
              )}
            </DialogContent>

            <DialogActions sx={{ p: 2, gap: 1, flexWrap: "wrap" }}>
              {hasOpenBills && (
                <Typography variant="body2" color="text.secondary" sx={{ mr: "auto" }}>
                  Close all bills to end the session.
                </Typography>
              )}
              <Button
                variant="outlined"
                startIcon={<ReceiptText size={18} />}
                onClick={() => actions.newBill(session)}
                disabled={!session.table_Numbers?.length}
              >
                New Bill
              </Button>
              <Button
                variant="contained"
                color="error"
                startIcon={<LogOut size={18} />}
                onClick={() => actions.endSession(session)}
                disabled={!session.is_Closable}
              >
                End Session
              </Button>
            </DialogActions>
          </>
        )}
      </Dialog>

      <BillSummaryDialog
        open={summaryBillId !== null}
        sessionId={session?.session_Id}
        billId={summaryBillId}
        onClose={() => setSummaryBillId(null)}
      />

      <ConfirmDialog
        open={!!billToClose}
        title="Close bill"
        message={`Close ${billToClose?.bill_Name ?? "this bill"}? Guests won't be able to order on it.`}
        confirmLabel="Close Bill"
        onConfirm={() => actions.closeBill(session, billToClose)}
        onClose={() => setBillToClose(null)}
      />
    </>
  );
};

export default SessionDetailDialog;
