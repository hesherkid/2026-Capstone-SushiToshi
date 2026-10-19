import { render, screen } from "@testing-library/react";
import BillBreakdown from "../BillBreakdown";

const summary = {
  bill_name: "Lee", status: "Open", pricing_type: "Dinner", created_at: "2026-10-18T04:00:00Z",
  adult_count: 2, adult_base_price: 42.95, child_count: 1, child_base_price: 28.95,
  base_charges_subtotal: 114.85,
  addon_items: [{ order_item_id: 5, item_name: "Wagyu Nigiri", quantity: 2, price_per_unit: 9, line_total: 18 }],
  addon_subtotal: 18, subtotal: 132.85, tax_rate: 0.05, tax_amount: 6.64, total_amount: 139.49,
};

describe("BillBreakdown", () => {
  it("lays the bill out as receipt lines, not tables", () => {
    render(<BillBreakdown summary={summary} />);

    expect(screen.queryByRole("table")).not.toBeInTheDocument();

    // Each guest type: name and amount, with count × price underneath
    expect(screen.getByText("Adults")).toBeInTheDocument();
    expect(screen.getByText("2 × $42.95")).toBeInTheDocument();
    expect(screen.getByText("$85.90")).toBeInTheDocument();
    expect(screen.getByText("1 × $28.95")).toBeInTheDocument();
    expect(screen.queryByText("Seniors")).not.toBeInTheDocument();
    expect(screen.getByText("$114.85")).toBeInTheDocument();

    expect(screen.getByText("Wagyu Nigiri")).toBeInTheDocument();
    expect(screen.getByText("2 × $9.00")).toBeInTheDocument();

    expect(screen.getByText("Tax (5%)")).toBeInTheDocument();
    expect(screen.getByText("Total")).toBeInTheDocument();
    expect(screen.getByText("$139.49")).toBeInTheDocument();
  });

  it("leaves out the add-ons section when there are none", () => {
    render(<BillBreakdown summary={{ ...summary, addon_items: [] }} />);

    expect(screen.queryByText("Premium Add-ons")).not.toBeInTheDocument();
  });
});
