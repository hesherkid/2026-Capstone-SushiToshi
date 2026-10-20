import { render, screen, fireEvent } from "@testing-library/react";
import CartLine from "../CartLine";

const misoSoup = { item_id: 11, name: "Miso Soup", price: 6, is_add_on: false };
const wagyu = { item_id: 13, name: "Wagyu Nigiri", price: 9, is_add_on: true, itemLimits: { perOrder: 2 } };

describe("Cart line", () => {
  it("shows just the name, quantity and that it's included", () => {
    render(<CartLine item={misoSoup} categoryId={1} quantity={2} onQuantityChange={jest.fn()} />);

    expect(screen.getByRole("heading", { name: "Miso Soup" })).toBeInTheDocument();
    expect(screen.getByText("Included")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
  });

  it("shows the add-on price and its limit", () => {
    render(<CartLine item={wagyu} categoryId={2} quantity={1} onQuantityChange={jest.fn()} />);

    expect(screen.getByText("$9.00 each")).toBeInTheDocument();
    expect(screen.getByText(/Add-on/)).toHaveTextContent("Add-on $9.00 each");
    expect(screen.queryByText("Included")).not.toBeInTheDocument();
    expect(screen.getByText("2 per order")).toBeInTheDocument();
  });

  it("changes the quantity with the stepper", () => {
    const onQuantityChange = jest.fn();
    render(<CartLine item={misoSoup} categoryId={1} quantity={2} onQuantityChange={onQuantityChange} />);

    fireEvent.click(screen.getByRole("button", { name: "Add one Miso Soup" }));
    fireEvent.click(screen.getByRole("button", { name: "Remove one Miso Soup" }));

    expect(onQuantityChange).toHaveBeenNthCalledWith(1, 1, 11, 1);
    expect(onQuantityChange).toHaveBeenNthCalledWith(2, 1, 11, -1);
  });

  it("shows a bin instead of a minus when one more tap takes the item out", () => {
    const { rerender } = render(
      <CartLine item={misoSoup} categoryId={1} quantity={2} onQuantityChange={jest.fn()} />,
    );
    expect(screen.getByRole("button", { name: "Remove one Miso Soup" })).toHaveTextContent("remove");

    rerender(<CartLine item={misoSoup} categoryId={1} quantity={1} onQuantityChange={jest.fn()} />);
    expect(screen.getByRole("button", { name: "Remove one Miso Soup" })).toHaveTextContent("delete");
  });
});
