import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import ConfirmDialog from "../ConfirmDialog";

describe("ConfirmDialog", () => {
  it("runs the action, then closes", async () => {
    const onConfirm = jest.fn().mockResolvedValue();
    const onClose = jest.fn();
    render(
      <ConfirmDialog open title="Delete table" message="Delete Table 4?" confirmLabel="Delete" onConfirm={onConfirm} onClose={onClose} />
    );

    expect(screen.getByRole("heading", { name: "Delete table" })).toBeInTheDocument();
    expect(screen.getByText("Delete Table 4?")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it("closes without running the action on Cancel", () => {
    const onConfirm = jest.fn();
    const onClose = jest.fn();
    render(<ConfirmDialog open title="Delete table" message="Delete Table 4?" onConfirm={onConfirm} onClose={onClose} />);

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("stays open and shows the error when the action returns false", async () => {
    const onConfirm = jest.fn().mockResolvedValue(false);
    const onClose = jest.fn();
    const { rerender } = render(
      <ConfirmDialog open title="Delete tag" message="Delete Spicy?" confirmLabel="Delete" onConfirm={onConfirm} onClose={onClose} />
    );

    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(onConfirm).toHaveBeenCalled());
    // The parent sets the error after the failed delete
    rerender(
      <ConfirmDialog open title="Delete tag" message="Delete Spicy?" confirmLabel="Delete" error="Tag not found" onConfirm={onConfirm} onClose={onClose} />
    );

    expect(screen.getByRole("alert")).toHaveTextContent("Tag not found");
    expect(screen.getByRole("button", { name: "Delete" })).toBeEnabled();
    expect(onClose).not.toHaveBeenCalled();
  });
});
