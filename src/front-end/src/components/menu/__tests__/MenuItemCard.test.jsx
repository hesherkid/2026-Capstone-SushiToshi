import { render, screen } from "@testing-library/react";
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
  categoryLimits: {},
  itemLimits: {},
  ...overrides,
});

describe("Menu item limits line", () => {
  it("is empty when nothing is limited", () => {
    expect(limitText(item())).toBe("");
  });

  it("shows the item's own per-order limit", () => {
    expect(limitText(item({ itemLimits: { perOrder: 1 } }))).toBe(
      "1 per order",
    );
  });

  it("names the section for a category's combine per-order limit", () => {
    expect(
      limitText(
        item({ category: "Sashimi", categoryLimits: { perOrder: 30 } }),
      ),
    ).toBe("30 per order across all Sashimi");
  });

  it("shows what an extra costs past a Charge limit, from the item's price", () => {
    const hotStone = item({
      name: "Hot Stone Steak",
      category: "Hot Stone",
      price: 5,
      categoryLimits: { adult: 1, overLimitBehavior: "Charge" },
    });

    expect(limitText(hotStone)).toBe("1 per person, then $5.00 each");
  });

  it("doesn't mention a charge when the item has no price", () => {
    expect(
      limitText(
        item({ categoryLimits: { adult: 1, overLimitBehavior: "Charge" } }),
      ),
    ).toBe("Limit 1 per person");
  });

  it("uses the tighter per-person limit, and its behavior", () => {
    // The item blocks at 1; the category would charge from 2, but the guest hits the item first
    const tighterItem = item({
      price: 5,
      itemLimits: { adult: 1, overLimitBehavior: "Block" },
      categoryLimits: { adult: 2, overLimitBehavior: "Charge" },
    });

    expect(limitText(tighterItem)).toBe("Limit 1 per person");
  });

  it("joins per-order and per-person limits", () => {
    const chopChop = item({
      itemLimits: { perOrder: 1, adult: 2, overLimitBehavior: "Block" },
    });

    expect(limitText(chopChop)).toBe("1 per order · Limit 2 per person");
  });
});

describe("Menu item card", () => {
  const nigiri = item({ itemLimits: { perOrder: 1 } });

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
    render(
      <MenuItemCard
        item={nigiri}
        categoryId={2}
        quantity={1}
        onQuantityChange={jest.fn()}
        atLimit
      />,
    );

    expect(
      screen.queryByRole("button", { name: "Add one Salmon Nigiri" }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Remove one Salmon Nigiri" }),
    ).toBeEnabled();
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
