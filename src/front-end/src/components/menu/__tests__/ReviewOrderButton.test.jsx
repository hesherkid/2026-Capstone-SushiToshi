import { render, screen, fireEvent, within } from "@testing-library/react";
import ReviewOrderButton from "../ReviewOrderButton";

describe("Review order button", () => {
  it("shows the item count and opens the cart when tapped", () => {
    const onClick = jest.fn();
    render(<ReviewOrderButton count={3} onClick={onClick} />);

    const button = screen.getByRole("button", { name: "Review order, 3 items" });
    expect(button).toHaveTextContent("Review order");
    expect(within(button).getByText("3")).toBeInTheDocument();

    fireEvent.click(button);
    expect(onClick).toHaveBeenCalled();
  });

  it("says item for a single item", () => {
    render(<ReviewOrderButton count={1} onClick={jest.fn()} />);

    expect(screen.getByRole("button", { name: "Review order, 1 item" })).toBeInTheDocument();
  });

  it("keeps big counts short enough to fit in the circle", () => {
    render(<ReviewOrderButton count={120} onClick={jest.fn()} />);

    expect(screen.getByText("99+")).toBeInTheDocument();
  });

  it("stays hidden while the cart is empty", () => {
    render(<ReviewOrderButton count={0} onClick={jest.fn()} />);

    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
