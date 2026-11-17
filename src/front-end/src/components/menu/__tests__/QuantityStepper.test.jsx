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

  it("takes a typed quantity on Enter or leaving the box", () => {
    const onSetQuantity = jest.fn();
    render(
      <QuantityStepper
        itemName="Miso Soup"
        quantity={2}
        onDecrease={jest.fn()}
        onIncrease={jest.fn()}
        onSetQuantity={onSetQuantity}
      />,
    );

    const box = screen.getByRole("textbox", { name: "Quantity of Miso Soup" });
    expect(box).toHaveValue("2");

    // Only digits go in
    fireEvent.change(box, { target: { value: "1a2" } });
    expect(box).toHaveValue("12");
    fireEvent.keyDown(box, { key: "Enter" });
    fireEvent.blur(box);
    expect(onSetQuantity).toHaveBeenCalledTimes(1);
    expect(onSetQuantity).toHaveBeenCalledWith(12);

    fireEvent.change(box, { target: { value: "5" } });
    fireEvent.blur(box);
    expect(onSetQuantity).toHaveBeenLastCalledWith(5);
  });

  it("puts the quantity back when the box is emptied or Escape is pressed", () => {
    const onSetQuantity = jest.fn();
    render(
      <QuantityStepper
        itemName="Miso Soup"
        quantity={2}
        onDecrease={jest.fn()}
        onIncrease={jest.fn()}
        onSetQuantity={onSetQuantity}
      />,
    );

    const box = screen.getByRole("textbox", { name: "Quantity of Miso Soup" });
    fireEvent.change(box, { target: { value: "" } });
    fireEvent.blur(box);
    expect(box).toHaveValue("2");

    fireEvent.change(box, { target: { value: "7" } });
    fireEvent.keyDown(box, { key: "Escape" });
    expect(box).toHaveValue("2");
    fireEvent.blur(box);
    expect(onSetQuantity).not.toHaveBeenCalled();
  });
});
