import { render, screen, fireEvent, within } from "@testing-library/react";
import CartDrawer from "../CartDrawer";

const bills = [
  { bill_id: 333, bill_name: "Park", requested_at: null },
  { bill_id: 334, bill_name: "Kim", requested_at: null },
  { bill_id: 332, bill_name: "Lee", requested_at: "2026-10-18T04:30:00Z" },
];

const renderCart = (props = {}) =>
  render(
    <CartDrawer
      open
      onClose={jest.fn()}
      categories={[]}
      quantities={{}}
      menuItems={[]}
      onQuantityChange={jest.fn()}
      onConfirmOrder={jest.fn()}
      isLoading={false}
      selectedBillId={333}
      bills={bills}
      onBillChange={jest.fn()}
      itemCount={1}
      totalQuantity={1}
      {...props}
    />
  );

describe("Cart bill choice", () => {
  it("lets the orderer choose which open bill the order goes on", () => {
    const onBillChange = jest.fn();
    renderCart({ onBillChange });

    fireEvent.mouseDown(screen.getByRole("combobox", { name: "Put this order on" }));
    const options = within(screen.getByRole("listbox"));
    expect(options.getByRole("option", { name: "Park" })).toBeInTheDocument();
    // Asked-for bills can't take more orders
    expect(options.getByRole("option", { name: "Lee (bill requested)" })).toHaveAttribute("aria-disabled", "true");

    fireEvent.click(options.getByRole("option", { name: "Kim" }));
    expect(onBillChange).toHaveBeenCalledWith(334);
  });

  it("just names the bill when there's only one", () => {
    renderCart({ bills: [bills[0]] });

    expect(screen.getByText("Ordering on Park")).toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Put this order on" })).not.toBeInTheDocument();
  });

  it("won't place an order on a requested bill", () => {
    renderCart({ selectedBillId: 332, orderingLocked: true, lockedMessage: "You've asked for your bill" });

    expect(screen.getByText("You've asked for your bill")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Place Order/ })).toBeDisabled();
  });
});
