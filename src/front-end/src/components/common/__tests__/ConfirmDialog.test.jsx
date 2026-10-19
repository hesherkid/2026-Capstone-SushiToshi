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
});
