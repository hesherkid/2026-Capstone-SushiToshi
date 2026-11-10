import { useState } from "react";
import {
  DialogContent,
  DialogActions,
  Box,
  Typography,
  Button,
  IconButton,
  Chip,
  Tooltip,
  Paper,
  Stack,
  Avatar,
} from "@mui/material";
import { alpha } from "@mui/material/styles";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import {
  Check,
  Receipt,
  ReceiptText,
  LogOut,
  Bell,
  Smartphone,
  Plus,
  Users,
  Pencil,
  ArrowRightLeft,
  AlertTriangle,
  Merge,
  Lock,
  LockOpen,
} from "lucide-react";
import { useSession } from "../../context/SessionContext";
import { useSessionDashboardActions } from "../../hooks/useSessionDashboardActions";
import {
  formatTime,
  formatBillStatus,
  getBillStatusColor,
  formatGuestBreakdown,
  sortBillsByStatus,
  sessionGuestCount,
} from "../../utils/sessionHelpers";
import { tableLabel, isOpenBill, SeatedFor } from "../SessionColumns";
import BillSummaryDialog from "./BillSummaryDialog";

const SectionHeading = ({ icon: Icon, children }) => (
  <Typography
    variant="subtitle2"
    component="h3"
    sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5, color: "text.secondary" }}
  >
    <Icon size={16} aria-hidden />
    {children}
  </Typography>
);

// One figure in the row under the title
const Stat = ({ label, children, tone }) => (
  <Box
    sx={(theme) => ({
      flex: "1 1 110px",
      px: 2,
      py: 1.25,
      borderRadius: 2,
      bgcolor: tone ? alpha(theme.palette[tone].main, 0.1) : "action.hover",
    })}
  >
    <Typography variant="caption" color="text.secondary" component="div">
      {label}
    </Typography>
    <Typography variant="subtitle1" component="div" fontWeight={600} color={tone ? `${tone}.dark` : undefined}>
      {children}
    </Typography>
  </Box>
);

const initialsOf = (name) =>
  (name || "?")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join("");

// The reasons staff should check an open bill's guest count, as chips
const guestCheckChips = (bill) => {
  if (!isOpenBill(bill)) return [];
  const chips = [];
  // A guest entered this party's size on their phone
  if (bill.started_by_guest) {
    chips.push(
      <Tooltip key="started" title="A guest started this bill on their phone. Check the number of guests.">
        <Chip icon={<Smartphone size={14} />} label="Started by guest" color="info" size="small" variant="outlined" />
      </Tooltip>
    );
  }
  if (bill.counts_changed_by_guest) {
    chips.push(
      <Tooltip key="changed" title="A guest changed this bill's guest count on their phone. Check the number of guests.">
        <Chip icon={<Pencil size={14} />} label="Count changed by guest" color="info" size="small" variant="outlined" />
      </Tooltip>
    );
  }
  // More people joined on their phones than the bill has guests
  if (bill.people_on_bill > bill.total_guests) {
    chips.push(
      <Tooltip
        key="people"
        title={`${bill.people_on_bill} people have joined this bill on their phones, but it's for ${bill.total_guests}. Check the number of guests.`}
      >
        <Chip
          icon={<Users size={14} />}
          label={`${bill.people_on_bill} people on ${bill.total_guests}-guest bill`}
          color="warning"
          size="small"
        />
      </Tooltip>
    );
  }
  return chips;
};

const SessionDetailDialog = () => {
  const { selectedSession: session, closeSession, requestsBySession, lockGuestCounts, isSaving } =
    useSession();
  const actions = useSessionDashboardActions();
  const [summaryBillId, setSummaryBillId] = useState(null);

  const requests = session ? requestsBySession[session.session_id] || [] : [];
  const bills = sortBillsByStatus(session?.bills || []);
  const openBillCount = bills.filter(isOpenBill).length;
  const hasOpenBills = openBillCount > 0;
  const guestCount = sessionGuestCount(session);
  // Everyone still seated, and which bill they're on
  const people = session?.people || [];
  const billNameOf = (billId) => bills.find((b) => b.bill_id === billId)?.bill_name;

  return (
    <>
      <AppDialog open={!!session} onClose={closeSession} maxWidth="md">
        {session && (
          <>
            <DialogHeader
              title={`Dining Session #${session.session_id} · ${tableLabel(session.table_numbers)}`}
              subtitle={`Started ${formatTime(session.started_at)}`}
              onClose={closeSession}
            />

            <DialogContent dividers>
              <Stack spacing={3}>
                <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap" }}>
                  <Stat label="Seated for">
                    <SeatedFor startedAt={session.started_at} />
                  </Stat>
                  <Stat label="Guests">{guestCount}</Stat>
                  <Stat label="Open bills">
                    {openBillCount} of {bills.length}
                  </Stat>
                  <Stat label="Requests" tone={requests.length > 0 ? "warning" : undefined}>
                    {requests.length}
                  </Stat>
                </Box>

                {requests.length > 0 && (
                  <Box component="section">
                    <SectionHeading icon={Bell}>Service requests ({requests.length})</SectionHeading>
                    <Stack spacing={1}>
                      {requests.map((request) => (
                        <Paper
                          key={request.request_id}
                          variant="outlined"
                          sx={(theme) => ({
                            display: "flex",
                            alignItems: "center",
                            gap: 2,
                            px: 2,
                            py: 1.25,
                            borderRadius: 2,
                            borderColor: "warning.main",
                            bgcolor: alpha(theme.palette.warning.main, 0.08),
                          })}
                        >
                          <Box sx={{ flex: 1, minWidth: 0 }}>
                            <Typography fontWeight={600}>{request.notes || "Requested a server"}</Typography>
                            <Typography variant="body2" color="text.secondary">
                              Table {request.table_number ?? "?"} · {formatTime(request.created_at)}
                            </Typography>
                          </Box>
                          <Tooltip title="Mark done">
                            <IconButton
                              aria-label="Mark request done"
                              color="success"
                              onClick={() => actions.completeRequest(request)}
                            >
                              <Check size={20} />
                            </IconButton>
                          </Tooltip>
                        </Paper>
                      ))}
                    </Stack>
                  </Box>
                )}

                <Box component="section">
                  <SectionHeading icon={Receipt}>Bills ({bills.length})</SectionHeading>
                  {bills.length === 0 ? (
                    <Paper variant="outlined" sx={{ p: 3, borderRadius: 2, borderStyle: "dashed", textAlign: "center" }}>
                      <Typography color="text.secondary">
                        No bills yet. Start one with New Bill.
                      </Typography>
                    </Paper>
                  ) : (
                    <Stack spacing={1.5}>
                      {bills.map((bill) => {
                        const open = isOpenBill(bill);
                        const checks = guestCheckChips(bill);
                        return (
                          <Paper
                            key={bill.bill_id}
                            variant="outlined"
                            sx={{
                              p: 2,
                              borderRadius: 2,
                              borderColor: checks.length > 0 ? "warning.main" : "divider",
                              bgcolor: open ? "background.paper" : "action.hover",
                            }}
                          >
                            {/* The buttons wrap under the name on phones */}
                            <Box sx={{ display: "flex", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
                              <Box sx={{ flex: "1 1 200px", minWidth: 0 }}>
                                <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                                  <Typography variant="subtitle1" component="h4" fontWeight={600}>
                                    {bill.bill_name}
                                  </Typography>
                                  <Chip
                                    label={formatBillStatus(bill.status)}
                                    color={getBillStatusColor(bill.status)}
                                    size="small"
                                  />
                                  {/* Guests pressed "Ready for Bill"; they can't order more on it */}
                                  {open && bill.requested_at && (
                                    <Chip icon={<Bell size={14} />} label="Asked for bill" color="warning" size="small" />
                                  )}
                                  {open && bill.counts_locked && (
                                    <Tooltip title="Staff checked the number of guests. Guests can't change it on their phones.">
                                      <Chip icon={<Lock size={14} />} label="Guests locked" size="small" variant="outlined" />
                                    </Tooltip>
                                  )}
                                </Box>
                                <Typography variant="body2" color="text.secondary">
                                  {formatGuestBreakdown(bill)}
                                </Typography>
                              </Box>
                              <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
                                {open && (
                                  <Button
                                    variant="contained"
                                    disableElevation
                                    aria-label={`Add order to ${bill.bill_name}`}
                                    onClick={() => actions.addOrder(session, bill)}
                                    startIcon={<Plus size={18} />}
                                  >
                                    Add Order
                                  </Button>
                                )}
                                {open && (
                                  <Button
                                    variant="outlined"
                                    aria-label={`Edit guests on ${bill.bill_name}`}
                                    onClick={() => actions.editGuests(session, bill)}
                                    startIcon={<Pencil size={18} />}
                                  >
                                    Edit Guests
                                  </Button>
                                )}
                                {/* Once checked, lock the count so guests can't change it; unlock to let them again */}
                                {open && (
                                  <Button
                                    variant="outlined"
                                    aria-label={`${bill.counts_locked ? "Unlock" : "Lock"} guests on ${bill.bill_name}`}
                                    onClick={() => lockGuestCounts(session.session_id, bill.bill_id, !bill.counts_locked)}
                                    disabled={isSaving}
                                    startIcon={bill.counts_locked ? <LockOpen size={18} /> : <Lock size={18} />}
                                  >
                                    {bill.counts_locked ? "Unlock Guests" : "Lock Guests"}
                                  </Button>
                                )}
                                {/* Everything on this bill moves to another, e.g. when the table wants one bill */}
                                {open && openBillCount > 1 && (
                                  <Button
                                    variant="outlined"
                                    aria-label={`Merge ${bill.bill_name} into another bill`}
                                    onClick={() => actions.mergeBill(session, bill)}
                                    startIcon={<Merge size={18} />}
                                  >
                                    Merge
                                  </Button>
                                )}
                                <Tooltip title="View bill summary">
                                  <Button
                                    variant="outlined"
                                    aria-label={`View ${bill.bill_name} summary`}
                                    onClick={() => setSummaryBillId(bill.bill_id)}
                                    startIcon={<Receipt size={18} />}
                                  >
                                    View Bill Summary
                                  </Button>
                                </Tooltip>
                              </Box>
                            </Box>

                            {/* Why this bill's guest count needs a look; Edit Guests clears the guest flags */}
                            {checks.length > 0 && (
                              <Box
                                sx={(theme) => ({
                                  mt: 1.5,
                                  px: 1.5,
                                  py: 1,
                                  borderRadius: 1.5,
                                  display: "flex",
                                  alignItems: "center",
                                  gap: 1,
                                  flexWrap: "wrap",
                                  bgcolor: alpha(theme.palette.warning.main, 0.08),
                                })}
                              >
                                <Typography
                                  variant="body2"
                                  fontWeight={600}
                                  color="warning.dark"
                                  sx={{ display: "flex", alignItems: "center", gap: 0.5 }}
                                >
                                  <AlertTriangle size={16} aria-hidden />
                                  Check guests
                                </Typography>
                                {checks}
                              </Box>
                            )}
                          </Paper>
                        );
                      })}
                    </Stack>
                  )}
                </Box>

                {/* Who's on which bill; staff move people to fix a bill or split one by person */}
                {people.length > 0 && (
                  <Box component="section">
                    <SectionHeading icon={Users}>People at the table ({people.length})</SectionHeading>
                    <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden" }}>
                      {people.map((person, index) => {
                        const billName = billNameOf(person.bill_id);
                        return (
                          <Box
                            key={person.participant_id}
                            sx={{
                              display: "flex",
                              alignItems: "center",
                              gap: 1.5,
                              px: 2,
                              py: 1.25,
                              borderTop: index > 0 ? 1 : 0,
                              borderColor: "divider",
                            }}
                          >
                            <Avatar sx={{ width: 36, height: 36, fontSize: 14, bgcolor: "primary.main" }} aria-hidden>
                              {initialsOf(person.user_name)}
                            </Avatar>
                            <Box sx={{ flex: 1, minWidth: 0 }}>
                              <Typography fontWeight={500} noWrap>
                                {person.user_name || "Guest"}
                              </Typography>
                              <Typography
                                variant="body2"
                                color={billName ? "text.secondary" : "warning.dark"}
                                noWrap
                              >
                                {billName ?? "Not on a bill yet"}
                              </Typography>
                            </Box>
                            {hasOpenBills && (
                              <Button
                                size="small"
                                aria-label={`Move ${person.user_name || "guest"}`}
                                onClick={() => actions.moveGuest(session, person)}
                                startIcon={<ArrowRightLeft size={16} />}
                              >
                                Move
                              </Button>
                            )}
                          </Box>
                        );
                      })}
                    </Paper>
                  </Box>
                )}
              </Stack>
            </DialogContent>

            <DialogActions sx={{ flexWrap: "wrap" }}>
              {hasOpenBills && (
                <Typography variant="body2" color="text.secondary" sx={{ mr: "auto" }}>
                  Close all bills to end the session.
                </Typography>
              )}
              <Button
                variant="outlined"
                startIcon={<ReceiptText size={18} />}
                onClick={() => actions.newBill(session)}
                disabled={!session.table_numbers?.length}
              >
                New Bill
              </Button>
              <Button
                variant="contained"
                color="error"
                startIcon={<LogOut size={18} />}
                onClick={() => actions.endSession(session)}
                disabled={!session.is_closable}
              >
                End Session
              </Button>
            </DialogActions>
          </>
        )}
      </AppDialog>

      <BillSummaryDialog
        open={summaryBillId !== null}
        sessionId={session?.session_id}
        billId={summaryBillId}
        onClose={() => setSummaryBillId(null)}
      />
    </>
  );
};

export default SessionDetailDialog;
