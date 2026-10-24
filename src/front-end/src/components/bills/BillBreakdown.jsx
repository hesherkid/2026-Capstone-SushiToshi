import { Box, Typography, Divider, Chip } from "@mui/material";
import { Lock } from "lucide-react";
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

// 0.05 -> "5%"
const percent = (rate) => `${Number((rate * 100).toFixed(2))}%`;

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

// One priced section of order lines (add-ons, over-limit); hidden when empty
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
 * A bill's priced breakdown: guests by age, add-ons, over-limit charges, leftovers, tax and total.
 * Shared by the staff and customer bill dialogs so both always show the same numbers.
 * showHeader = false leaves out the name, status and dates (the customer Bill page shows its own)
 */
const BillBreakdown = ({ summary, showHeader = true }) => (
  // Left-aligned like a receipt; pages sit in <main>, which centres text (globals.css)
  <Box sx={{ textAlign: "left" }}>
    {showHeader && (
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
            label={
              summary.holiday_name
                ? `${formatPricingType(summary.pricing_type)}: ${summary.holiday_name}`
                : formatPricingType(summary.pricing_type)
            }
            variant="outlined"
            size="small"
          />
          {/* Closed bills keep the prices they closed with */}
          {summary.prices_locked && (
            <Chip icon={<Lock size={14} />} label="Prices locked" variant="outlined" size="small" />
          )}
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
    )}

    {/* Laid out like a receipt: guests, add-ons, then the totals */}
    {showHeader && <ReceiptRule />}

    <SectionHeading>Guests</SectionHeading>
    {/* The customer page has no header chips, so say why the prices are higher here */}
    {!showHeader && summary.holiday_name && (
      <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>
        Holiday prices for {summary.holiday_name}
      </Typography>
    )}
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

    <ChargeSection
      title="Premium Add-ons"
      items={summary.addon_items}
      subtotalLabel="Add-ons"
      subtotal={summary.addon_subtotal}
    />
    <ChargeSection
      title="Over Limit Charges"
      items={summary.overlimit_items}
      subtotalLabel="Over limit"
      subtotal={summary.overlimit_subtotal}
    />

    {summary.leftover_charge > 0 && (
      <>
        <ReceiptRule />
        <SectionHeading>Leftovers</SectionHeading>
        <ReceiptLine
          label="Leftover charge (table, split evenly across bills)"
          amount={summary.leftover_charge}
          bold
        />
      </>
    )}

    <ReceiptRule />

    <ReceiptLine label="Subtotal" amount={summary.subtotal} />
    {/* Automatic for big tables (6 or more guests by default) */}
    {summary.gratuity_amount > 0 && (
      <Box>
        <ReceiptLine
          label={`Gratuity (${percent(summary.gratuity_rate)})`}
          amount={summary.gratuity_amount}
        />
        <Typography variant="body2" color="text.secondary" sx={{ mt: -0.25, mb: 0.5 }}>
          Added for parties of {summary.gratuity_min_party_size} or more (your table has{" "}
          {summary.party_size})
        </Typography>
      </Box>
    )}
    <ReceiptLine label={`GST (${percent(summary.tax_rate)})`} amount={summary.tax_amount} />

    <ReceiptRule />

    <ReceiptLine label="Total" amount={summary.total_amount} bold large />
  </Box>
);

export default BillBreakdown;
