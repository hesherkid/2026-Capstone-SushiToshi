import { render, screen, fireEvent } from "@testing-library/react";
import MenuItemCard, { limitText } from "../MenuItemCard";

jest.mock("@/components/Tags", () => ({
  __esModule: true,
  default: () => null,
}));

const item = (overrides) => ({
  item_id: 1,
  name: "Salmon Nigiri",
  price: 0,
  is_add_on: false,
  category: "Nigiri",
  category_limits: {},
  item_limits: {},
  ...overrides,
});

describe("Menu item limits line", () => {
  it("is empty when nothing is limited", () => {
    expect(limitText(item())).toBe("");
  });

  it("shows the item's own per-order limit", () => {
    expect(limitText(item({ item_limits: { per_order: 1 } }))).toBe(
      "1 per order",
    );
  });

  it("names the section for a category's combine per-order limit", () => {
    expect(
      limitText(
        item({ category: "Sashimi", category_limits: { per_order: 30 } }),
      ),
    ).toBe("30 per order across all Sashimi");
  });

  it("shows what an extra costs past a Charge limit, from the item's price", () => {
    const hotStone = item({
      name: "Hot Stone Steak",
      category: "Hot Stone",
      price: 5,
      category_limits: { adult: 1, over_limit_behavior: "Charge" },
    });

    expect(limitText(hotStone)).toBe("1 per person, then $5.00 each");
  });

  it("doesn't mention a charge when the item has no price", () => {
    expect(
      limitText(
        item({ category_limits: { adult: 1, over_limit_behavior: "Charge" } }),
      ),
    ).toBe("Limit 1 per person");
  });

  it("uses the tighter per-person limit, and its behavior", () => {
    // The item blocks at 1; the category would charge from 2, but the guest hits the item first
    const tighterItem = item({
      price: 5,
      item_limits: { adult: 1, over_limit_behavior: "Block" },
      category_limits: { adult: 2, over_limit_behavior: "Charge" },
    });

    expect(limitText(tighterItem)).toBe("Limit 1 per person");
  });

  it("joins per-order and per-person limits", () => {
    const chopChop = item({
      item_limits: { per_order: 1, adult: 2, over_limit_behavior: "Block" },
    });

    expect(limitText(chopChop)).toBe("1 per order · Limit 2 per person");
  });
});

describe("Menu item card", () => {
  const nigiri = item({ item_limits: { per_order: 1 } });

  it("shows the limits line on the card", () => {
    render(
      <MenuItemCard
        item={nigiri}
        categoryId={2}
        quantity={0}
        onQuantityChange={jest.fn()}
      />,
    );

    expect(screen.getByText("1 per order")).toBeInTheDocument();
    expect(screen.getByText("Included")).toBeInTheDocument();
  });

  it("stops adding once the cart holds as many as the limit allows", () => {
    const onQuantityChange = jest.fn();
    render(
      <MenuItemCard
        item={nigiri}
        categoryId={2}
        quantity={1}
        onQuantityChange={onQuantityChange}
        atLimit
      />,
    );

    const add = screen.getByRole("button", {
      name: "Add one Salmon Nigiri, limit reached",
    });
    expect(add).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(add);
    expect(onQuantityChange).not.toHaveBeenCalled();

    const remove = screen.getByRole("button", { name: "Remove Salmon Nigiri" });
    expect(remove).not.toHaveAttribute("aria-disabled", "true");
    fireEvent.click(remove);
    expect(onQuantityChange).toHaveBeenCalledWith(2, nigiri.item_id, -1);
  });

  it("shows an add-on's price with two decimals", () => {
    render(
      <MenuItemCard
        item={item({ name: "Toro Nigiri", is_add_on: true, price: 1 })}
        categoryId={9}
        quantity={0}
        onQuantityChange={jest.fn()}
      />,
    );

    expect(screen.getByText("$1.00")).toBeInTheDocument();
  });
});
