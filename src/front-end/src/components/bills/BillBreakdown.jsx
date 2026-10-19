import {
  Box,
  Typography,
  Divider,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
} from "@mui/material";
import {
  formatCurrency,
  formatDateTime,
  formatPricingType,
  formatBillStatus,
  getBillStatusColor,
} from "@/components/staff/SessionDashboard/utils/sessionHelpers";

const headSx = {
  backgroundColor: "primary.main",
  "& .MuiTableCell-head": { color: "white", fontWeight: 600 },
};

// Field names from GET /Bill/summary
const GUEST_TYPES = [
  { label: "Adults", count: "adult_count", price: "adult_base_price" },
  { label: "Seniors", count: "senior_count", price: "senior_base_price" },
  { label: "Children", count: "child_count", price: "child_base_price" },
  { label: "Toddlers", count: "tot_count", price: "toddler_base_price" },
];

const SubtotalRow = ({ label, amount }) => (
  <TableRow>
    <TableCell colSpan={3} align="right" sx={{ fontWeight: 600 }}>
      {label}
    </TableCell>
    <TableCell align="right" sx={{ fontWeight: 600 }}>
      {formatCurrency(amount)}
    </TableCell>
  </TableRow>
);

const TotalLine = ({ label, amount }) => (
  <Box display="flex" justifyContent="space-between" sx={{ mb: 1 }}>
    <Typography variant="body1">{label}</Typography>
    <Typography variant="body1">{formatCurrency(amount)}</Typography>
  </Box>
);

/**
 * A bill's priced breakdown: guests by age, add-ons, tax and total.
 * Shared by the staff and customer bill dialogs so both always show the same numbers.
 */
const BillBreakdown = ({ summary }) => (
  <Box>
    <Box sx={{ mb: 3 }}>
      <Typography variant="h6" gutterBottom>
        {summary.bill_name}
      </Typography>
      <Box display="flex" gap={2} flexWrap="wrap" alignItems="center">
        <Chip
          label={formatBillStatus(summary.status)}
          color={getBillStatusColor(summary.status)}
          size="small"
        />
        <Chip label={formatPricingType(summary.pricing_type)} variant="outlined" size="small" />
        <Typography variant="body2" color="text.secondary">
          Created: {formatDateTime(summary.created_at)}
        </Typography>
        {summary.table_numbers?.length > 0 && (
          <Typography variant="body2" color="text.secondary">
            Tables: {summary.table_numbers.join(", ")}
          </Typography>
        )}
      </Box>
    </Box>

    <Divider sx={{ my: 2 }} />

    <Box sx={{ mb: 3 }}>
      <Typography variant="subtitle1" fontWeight="600" gutterBottom>
        Guest Breakdown
      </Typography>
      <TableContainer component={Paper} variant="outlined">
        <Table size="small">
          <TableHead sx={headSx}>
            <TableRow>
              <TableCell>Type</TableCell>
              <TableCell align="right">Count</TableCell>
              <TableCell align="right">Price Each</TableCell>
              <TableCell align="right">Subtotal</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {GUEST_TYPES.filter((type) => summary[type.count] > 0).map((type) => (
              <TableRow key={type.label}>
                <TableCell>{type.label}</TableCell>
                <TableCell align="right">{summary[type.count]}</TableCell>
                <TableCell align="right">{formatCurrency(summary[type.price])}</TableCell>
                <TableCell align="right">
                  {formatCurrency(summary[type.count] * summary[type.price])}
                </TableCell>
              </TableRow>
            ))}
            <SubtotalRow label="Base Charges Subtotal:" amount={summary.base_charges_subtotal} />
          </TableBody>
        </Table>
      </TableContainer>
    </Box>

    {summary.addon_items?.length > 0 && (
      <Box sx={{ mb: 3 }}>
        <Typography variant="subtitle1" fontWeight="600" gutterBottom>
          Premium Add-ons
        </Typography>
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead sx={headSx}>
              <TableRow>
                <TableCell>Item</TableCell>
                <TableCell align="right">Qty</TableCell>
                <TableCell align="right">Price</TableCell>
                <TableCell align="right">Total</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {summary.addon_items.map((item) => (
                <TableRow key={item.order_item_id}>
                  <TableCell>{item.item_name}</TableCell>
                  <TableCell align="right">{item.quantity}</TableCell>
                  <TableCell align="right">{formatCurrency(item.price_per_unit)}</TableCell>
                  <TableCell align="right">{formatCurrency(item.line_total)}</TableCell>
                </TableRow>
              ))}
              <SubtotalRow label="Add-ons Subtotal:" amount={summary.addon_subtotal} />
            </TableBody>
          </Table>
        </TableContainer>
      </Box>
    )}

    <Divider sx={{ my: 2 }} />

    <Box>
      <TotalLine label="Subtotal:" amount={summary.subtotal} />
      <TotalLine label={`Tax (${(summary.tax_rate * 100).toFixed(0)}%):`} amount={summary.tax_amount} />
      <Divider sx={{ my: 1 }} />
      <Box display="flex" justifyContent="space-between">
        <Typography variant="h6" fontWeight="700">
          Total Amount:
        </Typography>
        <Typography variant="h6" fontWeight="700" color="primary">
          {formatCurrency(summary.total_amount)}
        </Typography>
      </Box>
    </Box>
  </Box>
);

export default BillBreakdown;
