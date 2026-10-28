import { render, screen, fireEvent, within } from "@testing-library/react";
import { TextField } from "@mui/material";
import FormDialog from "../FormDialog";

const renderDialog = (props = {}) => {
  const handlers = { onClose: jest.fn(), onSubmit: jest.fn(), onErrorClose: jest.fn() };
  render(
    <FormDialog open title="Edit Table" subtitle="Table 4" submitLabel="Save Changes" {...handlers} {...props}>
      <TextField label="Seat Count" required defaultValue="4" />
    </FormDialog>
  );
  return handlers;
};

describe("FormDialog", () => {
  it("shows the title, subtitle, fields, Cancel and the save button", () => {
    renderDialog();

    expect(screen.getByRole("heading", { name: "Edit Table" })).toBeInTheDocument();
    expect(screen.getByText("Table 4")).toBeInTheDocument();
    expect(screen.getByLabelText(/Seat Count/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Cancel" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save Changes" })).toBeEnabled();
  });

  it("saves from the button and from Enter in a field", () => {
    const { onSubmit } = renderDialog();

    fireEvent.click(screen.getByRole("button", { name: "Save Changes" }));
    fireEvent.submit(screen.getByLabelText(/Seat Count/).closest("form"));

    expect(onSubmit).toHaveBeenCalledTimes(2);
  });

  it("closes from Cancel and the close button", () => {
    const { onClose } = renderDialog();

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    fireEvent.click(screen.getByRole("button", { name: "Close" }));

    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it("shows the error and lets it be dismissed", () => {
    const { onErrorClose } = renderDialog({ error: "Couldn't update the table." });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Couldn't update the table.");
    fireEvent.click(within(alert).getByRole("button"));
    expect(onErrorClose).toHaveBeenCalled();
  });

  it("while busy shows busyLabel and can't be saved again or closed", () => {
    const { onSubmit, onClose } = renderDialog({ busy: true, busyLabel: "Saving..." });

    const save = screen.getByRole("button", { name: "Saving..." });
    expect(save).toBeDisabled();
    expect(screen.getByRole("button", { name: "Cancel" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Close" })).toBeDisabled();

    fireEvent.submit(save.closest("form"));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it("submitDisabled keeps the save button off", () => {
    const { onSubmit } = renderDialog({ submitDisabled: true });

    const save = screen.getByRole("button", { name: "Save Changes" });
    expect(save).toBeDisabled();
    fireEvent.submit(save.closest("form"));
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
