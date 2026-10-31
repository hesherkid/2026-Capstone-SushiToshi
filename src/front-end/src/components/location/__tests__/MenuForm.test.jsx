import { render, screen } from "@testing-library/react";
import MenuForm from "../MenuForm";

const renderForm = (props) =>
  render(<MenuForm onSubmit={jest.fn()} onClose={jest.fn()} {...props} />);

const activeSwitch = () => screen.getByLabelText("Is Active Menu");

describe("MenuForm", () => {
  it("starts a new menu as active, matching the API default", () => {
    renderForm({ mode: "create", initialData: null });

    expect(activeSwitch().checked).toBe(true);
  });

  it("keeps an inactive menu inactive when editing", () => {
    renderForm({ mode: "edit", initialData: { name: "Late Night", is_active: false } });

    expect(activeSwitch().checked).toBe(false);
  });

  it("keeps an active menu active when editing", () => {
    renderForm({ mode: "edit", initialData: { name: "Lunch", is_active: true } });

    expect(activeSwitch().checked).toBe(true);
  });
});
