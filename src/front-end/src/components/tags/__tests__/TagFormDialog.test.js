import React from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import TagFormDialog from "../TagFormDialog";

jest.mock("@/utils/colorGenerator", () => ({
  generateColorSet: jest.fn(() => ["#aaaaaa", "#bbbbbb", "#cccccc"]),
}));

const renderDialog = (props = {}) => {
  const onSave = props.onSave ?? jest.fn().mockResolvedValue(undefined);
  render(
    <TagFormDialog
      open
      mode="add"
      tag={null}
      existingColors={[]}
      onClose={jest.fn()}
      {...props}
      onSave={onSave}
    />,
  );
  return { onSave, user: userEvent.setup() };
};

describe("TagFormDialog", () => {
  it("loads with the first suggested colour", async () => {
    const { onSave, user } = renderDialog();
    await user.type(screen.getByLabelText("Tag Name"), "Spicy");
    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(onSave).toHaveBeenCalledWith({
      tag_name: "Spicy",
      tag_color: "#aaaaaa",
    });
  });

  it("asks for a name instead of saving when the name is blank", async () => {
    const { onSave, user } = renderDialog();
    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(screen.getByText("Tag name is required")).toBeInTheDocument();
    expect(onSave).not.toHaveBeenCalled();
  });

  it("hides the name message once a name is typed", async () => {
    const { user } = renderDialog();
    await user.click(screen.getByRole("button", { name: "Save" }));
    await user.type(screen.getByLabelText("Tag Name"), "Spicy");
    expect(screen.queryByText("Tag name is required")).not.toBeInTheDocument();
  });

  it("shows the server's message when saving fails", async () => {
    const onSave = jest.fn().mockRejectedValue({
      response: { data: { detail: "Tag already exists" } },
    });
    const { user } = renderDialog({ onSave });
    await user.type(screen.getByLabelText("Tag Name"), "Spicy");
    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(await screen.findByText("Tag already exists")).toBeInTheDocument();
  });

  it("uses the picked colour", async () => {
    const { onSave, user } = renderDialog();
    await user.type(screen.getByLabelText("Tag Name"), "Spicy");
    await user.click(screen.getByLabelText("Color"));
    await user.click(screen.getByText("#bbbbbb"));
    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(onSave).toHaveBeenCalledWith({
      tag_name: "Spicy",
      tag_color: "#bbbbbb",
    });
  });

  it("fills in an existing tag and sends its id when updating", async () => {
    const tag = { tag_id: 7, tag_name: "Raw", tag_color: "#123456" };
    const { onSave, user } = renderDialog({ mode: "update", tag });
    expect(screen.getByLabelText("Tag Name")).toHaveValue("Raw");
    await user.click(screen.getByRole("button", { name: "Update" }));
    expect(onSave).toHaveBeenCalledWith(tag);
  });
});
