import { Box, Typography, Divider, Chip } from "@mui/material";
import {
  formatCurrency,
  formatDateTime,
  formatPricingType,
  formatBillStatus,
  getBillStatusColor,
} from "@/components/staff/SessionDashboard/utils/sessionHelpers";

// Field names from GET /Bill/summary
const GUEST_TYPES = [
  { label: "Adults", count: "adult_count", price: "adult_base_price" },
  { label: "Seniors", count: "senior_count", price: "senior_base_price" },
  { label: "Children", count: "child_count", price: "child_base_price" },
  { label: "Toddlers", count: "tot_count", price: "toddler_base_price" },
];

// Dashed rule between receipt sections
const ReceiptRule = () => <Divider sx={{ my: 1.5, borderStyle: "dashed" }} />;

const SectionHeading = ({ children }) => (
  <Typography
    variant="overline"
    component="h3"
    color="text.secondary"
    sx={{ display: "block", lineHeight: 2, letterSpacing: 1 }}
  >
    {children}
  </Typography>
);

// One receipt line: name and amount, with "2 × $42.95" underneath when there's a quantity
const ReceiptLine = ({ label, amount, quantity, unitPrice, bold, large }) => (
  <Box sx={{ py: 0.5 }}>
    <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2 }}>
      <Typography
        variant={large ? "h6" : "body1"}
        fontWeight={bold ? 700 : 400}
      >
        {label}
      </Typography>
      <Typography
        variant={large ? "h6" : "body1"}
        fontWeight={bold ? 700 : 400}
        color={large ? "primary" : undefined}
        sx={{ fontVariantNumeric: "tabular-nums", whiteSpace: "nowrap" }}
      >
        {formatCurrency(amount)}
      </Typography>
    </Box>
    {quantity != null && (
      <Typography
        variant="body2"
        color="text.secondary"
        sx={{ fontVariantNumeric: "tabular-nums" }}
      >
        {quantity} × {formatCurrency(unitPrice)}
      </Typography>
    )}
  </Box>
);

// One priced section (add-ons, over-limit, leftovers); hidden when empty
const ChargeSection = ({ title, items, subtotalLabel, subtotal }) =>
  items?.length > 0 && (
    <>
      <ReceiptRule />
      <SectionHeading>{title}</SectionHeading>
      {items.map((item) => (
        <ReceiptLine
          key={item.order_item_id}
          label={item.item_name}
          quantity={item.quantity}
          unitPrice={item.price_per_unit}
          amount={item.line_total}
        />
      ))}
      <ReceiptLine label={subtotalLabel} amount={subtotal} bold />
    </>
  );

/**
 * A bill's priced breakdown: guests by age, add-ons, tax and total.
 * Shared by the staff and customer bill dialogs so both always show the same numbers.
 */
const BillBreakdown = ({ summary }) => (
  <Box>
    <Box sx={{ mb: 2 }}>
      <Typography variant="h6" gutterBottom>
        {summary.bill_name}
      </Typography>
      <Box display="flex" gap={2} flexWrap="wrap" alignItems="center">
        <Chip
          label={formatBillStatus(summary.status)}
          color={getBillStatusColor(summary.status)}
          size="small"
        />
        <Chip
          label={formatPricingType(summary.pricing_type)}
          variant="outlined"
          size="small"
        />
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

    {/* Laid out like a receipt: guests, add-ons, then the totals */}
    <ReceiptRule />

    <SectionHeading>Guests</SectionHeading>
    {GUEST_TYPES.filter((type) => summary[type.count] > 0).map((type) => (
      <ReceiptLine
        key={type.label}
        label={type.label}
        quantity={summary[type.count]}
        unitPrice={summary[type.price]}
        amount={summary[type.count] * summary[type.price]}
      />
    ))}
    <ReceiptLine
      label="Base charges"
      amount={summary.base_charges_subtotal}
      bold
    />

    {(summary.addon_items?.length > 0 ||
      summary.overlimit_items?.length > 0) && (
      <>
        <ReceiptRule />
        <SectionHeading>Premium Add-ons</SectionHeading>
        {summary.addon_items?.map((item) => (
          <ReceiptLine
            key={item.order_item_id}
            label={item.item_name}
            quantity={item.quantity}
            unitPrice={item.price_per_unit}
            amount={item.line_total}
          />
        ))}
        <SectionHeading>Over Limit Charges</SectionHeading>
        {summary.overlimit_items?.map((item) => (
          <ReceiptLine
            key={`over-${item.order_item_id}`}
            label={`${item.item_name}`}
            quantity={item.quantity}
            unitPrice={item.price_per_unit}
            amount={item.line_total}
          />
        ))}
        <ReceiptLine
          label="Add-ons"
          amount={
            (summary.addon_subtotal ?? 0) + (summary.overlimit_subtotal ?? 0)
          }
          bold
        />
      </>
    )}

    <ReceiptRule />

    <ReceiptLine label="Subtotal" amount={summary.subtotal} />
    <ReceiptLine
      label={`Tax (${(summary.tax_rate * 100).toFixed(0)}%)`}
      amount={summary.tax_amount}
    />

    <ReceiptRule />

    <ReceiptLine label="Total" amount={summary.total_amount} bold large />
  </Box>
);

export default BillBreakdown;
