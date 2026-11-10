import { render, screen, fireEvent } from "@testing-library/react";
import QuantityStepper from "../QuantityStepper";

describe("Quantity stepper", () => {
  it("can't go below zero on the menu", () => {
    const onDecrease = jest.fn();
    const onIncrease = jest.fn();
    render(
      <QuantityStepper
        itemName="Miso Soup"
        quantity={0}
        onDecrease={onDecrease}
        onIncrease={onIncrease}
      />,
    );

    const remove = screen.getByRole("button", { name: "Remove one Miso Soup" });
    expect(remove).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(remove);
    expect(onDecrease).not.toHaveBeenCalled();

    const add = screen.getByRole("button", { name: "Add one Miso Soup" });
    expect(add).not.toHaveAttribute("aria-disabled", "true");
    fireEvent.click(add);
    expect(onIncrease).toHaveBeenCalledTimes(1);
  });

  it("adds and removes one at a time", () => {
    const onDecrease = jest.fn();
    const onIncrease = jest.fn();
    render(
      <QuantityStepper
        itemName="Miso Soup"
        quantity={2}
        onDecrease={onDecrease}
        onIncrease={onIncrease}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Add one Miso Soup" }));
    fireEvent.click(
      screen.getByRole("button", { name: "Remove one Miso Soup" }),
    );

    expect(onIncrease).toHaveBeenCalledTimes(1);
    expect(onDecrease).toHaveBeenCalledTimes(1);
  });

  it("greys out the plus once the item's limit is reached, but keeps it", () => {
    const onIncrease = jest.fn();
    render(
      <QuantityStepper
        itemName="Miso Soup"
        quantity={2}
        onDecrease={jest.fn()}
        onIncrease={onIncrease}
        canIncrease={false}
      />,
    );

    // Still there, so keyboard focus isn't lost, but it does nothing and looks disabled
    const add = screen.getByRole("button", {
      name: "Add one Miso Soup, limit reached",
    });
    expect(add).toHaveAttribute("aria-disabled", "true");
    expect(add).toHaveStyle({ cursor: "not-allowed" });
    fireEvent.click(add);
    expect(onIncrease).not.toHaveBeenCalled();

    // The minus still works
    const remove = screen.getByRole("button", { name: "Remove one Miso Soup" });
    expect(remove).not.toHaveAttribute("aria-disabled", "true");
    expect(remove).not.toHaveStyle({ cursor: "not-allowed" });
  });
});
