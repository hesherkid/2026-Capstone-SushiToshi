import { render, screen, fireEvent } from "@testing-library/react";
import QuantityStepper from "../QuantityStepper";

describe("Quantity stepper", () => {
  it("can't go below zero on the menu", () => {
    render(<QuantityStepper itemName="Miso Soup" quantity={0} onDecrease={jest.fn()} onIncrease={jest.fn()} />);

    expect(screen.getByRole("button", { name: "Remove one Miso Soup" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Add one Miso Soup" })).toBeEnabled();
  });

  it("adds and removes one at a time", () => {
    const onDecrease = jest.fn();
    const onIncrease = jest.fn();
    render(<QuantityStepper itemName="Miso Soup" quantity={2} onDecrease={onDecrease} onIncrease={onIncrease} />);

    fireEvent.click(screen.getByRole("button", { name: "Add one Miso Soup" }));
    fireEvent.click(screen.getByRole("button", { name: "Remove one Miso Soup" }));

    expect(onIncrease).toHaveBeenCalledTimes(1);
    expect(onDecrease).toHaveBeenCalledTimes(1);
  });
});
