import { render, screen } from "@testing-library/react";
import BillBreakdown from "../BillBreakdown";

const summary = {
  bill_name: "Lee",
  status: "Open",
  pricing_type: "Dinner",
  created_at: "2026-10-18T04:00:00Z",
  adult_count: 2,
  adult_base_price: 42.95,
  child_count: 1,
  child_base_price: 28.95,
  base_charges_subtotal: 114.85,
  addon_items: [
    {
      order_item_id: 5,
      item_name: "Wagyu Nigiri",
      quantity: 2,
      price_per_unit: 9,
      line_total: 18,
    },
  ],
  addon_subtotal: 18,
  subtotal: 132.85,
  tax_rate: 0.05,
  tax_amount: 6.64,
  total_amount: 139.49,
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

    expect(screen.getByText("GST (5%)")).toBeInTheDocument();
    expect(screen.queryByText(/Gratuity/)).not.toBeInTheDocument();
    expect(screen.getByText("Total")).toBeInTheDocument();
    expect(screen.getByText("$139.49")).toBeInTheDocument();
  });

  it("leaves out the add-ons section when there are none", () => {
    render(<BillBreakdown summary={{ ...summary, addon_items: [] }} />);

    expect(screen.queryByText("Premium Add-ons")).not.toBeInTheDocument();
  });

  it("only shows over-limit charges when there are some", () => {
    render(<BillBreakdown summary={summary} />);

    expect(screen.getByText("Premium Add-ons")).toBeInTheDocument();
    expect(screen.queryByText("Over Limit Charges")).not.toBeInTheDocument();
  });

  it("adds the gratuity line for a big table, with why", () => {
    render(
      <BillBreakdown
        summary={{
          ...summary,
          party_size: 8,
          gratuity_rate: 0.18,
          gratuity_amount: 23.91,
          gratuity_min_party_size: 6,
        }}
      />,
    );

    expect(screen.getByText("Gratuity (18%)")).toBeInTheDocument();
    expect(screen.getByText("$23.91")).toBeInTheDocument();
    expect(
      screen.getByText("Added for parties of 6 or more (your table has 8)"),
    ).toBeInTheDocument();
  });

  // One fee for the table's leftovers, split across its bills (not per item)
  it("shows the bill's share of the leftover fee", () => {
    render(<BillBreakdown summary={{ ...summary, leftover_charge: 7.5 }} />);

    expect(
      screen.getByText("Leftovers", { selector: "h3" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Leftover charge (table, split evenly across bills)"),
    ).toBeInTheDocument();
    expect(screen.getByText("$7.50")).toBeInTheDocument();
  });

  it("leaves out leftovers when there's no fee", () => {
    render(<BillBreakdown summary={{ ...summary, leftover_charge: 0 }} />);

    expect(
      screen.queryByText("Leftovers", { selector: "h3" }),
    ).not.toBeInTheDocument();
  });

  it("lists over-limit charges in their own section with their own subtotal", () => {
    const hotStoneExtra = {
      order_item_id: 6,
      item_name: "Hot Stone Steak",
      quantity: 1,
      price_per_unit: 5,
      line_total: 5,
    };
    render(
      <BillBreakdown
        summary={{
          ...summary,
          overlimit_items: [hotStoneExtra],
          overlimit_subtotal: 5,
        }}
      />,
    );

    expect(screen.getByText("Over Limit Charges")).toBeInTheDocument();
    expect(screen.getByText("Hot Stone Steak")).toBeInTheDocument();
    expect(screen.getByText("1 × $5.00")).toBeInTheDocument();
    expect(screen.getByText("Over limit").nextSibling).toHaveTextContent(
      "$5.00",
    );
    expect(screen.getByText("Add-ons").nextSibling).toHaveTextContent("$18.00");
  });
});
