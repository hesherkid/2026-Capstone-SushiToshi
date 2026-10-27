import { render, screen, fireEvent, within } from "@testing-library/react";
import MenuItemForm from "../MenuItemForm";

const renderForm = (props = {}) =>
  render(
    <MenuItemForm
      open
      onClose={jest.fn()}
      selectedItem={null}
      categories={[{ category_id: 1, category_name: "Nigiri" }]}
      imagePreview=""
      onImageChange={jest.fn()}
      onSubmit={jest.fn()}
      submitting={false}
      {...props}
    />
  );

describe("MenuItemForm", () => {
  it("shows a save error that can be dismissed", () => {
    const onErrorClose = jest.fn();
    renderForm({ error: "Failed to save menu item", onErrorClose });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Failed to save menu item");
    fireEvent.click(within(alert).getByRole("button"));

    expect(onErrorClose).toHaveBeenCalled();
  });
});
