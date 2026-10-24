import {
  render,
  screen,
  fireEvent,
  waitFor,
  within,
} from "@testing-library/react";
import TagsDialog from "../TagsDialog";

const TAGS = [
  { tag_id: 3, tag_name: "Spicy", tag_color: "#FF5733" },
  { tag_id: 4, tag_name: "Vegan", tag_color: "#33FF57" },
];
const ITEM = {
  item_id: 7,
  name: "Salmon Nigiri",
  tags: [{ tag_id: 3, name: "Spicy", color_code: "#FF5733" }],
};

const renderDialog = (onTagAction, props = {}) =>
  render(
    <TagsDialog
      open
      onClose={jest.fn()}
      selectedItem={ITEM}
      tags={TAGS}
      onTagAction={onTagAction}
      {...props}
    />,
  );

const section = (heading) =>
  within(screen.getByText(heading).nextElementSibling);
const clickTag = (name) =>
  fireEvent.click(screen.getByRole("button", { name }));

describe("Tags dialog", () => {
  it("moves a tag to Applied once it's added", async () => {
    const onTagAction = jest.fn().mockResolvedValue();
    renderDialog(onTagAction);

    clickTag("Vegan");

    await waitFor(() =>
      expect(section("Applied Tags").getByText("Vegan")).toBeInTheDocument(),
    );
    expect(onTagAction).toHaveBeenCalledWith(7, 4, "add");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("shows the server's reason and leaves the tag where it was when adding fails", async () => {
    const onTagAction = jest.fn().mockRejectedValue({
      response: { status: 409, data: "Tag already assigned to this menu item" },
    });
    renderDialog(onTagAction);

    clickTag("Vegan");

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Tag already assigned to this menu item",
    );
    expect(section("Available Tags").getByText("Vegan")).toBeInTheDocument();
  });

  it("falls back to a general message when removing fails without a reason", async () => {
    const onTagAction = jest.fn().mockRejectedValue(new Error("Network Error"));
    renderDialog(onTagAction);

    clickTag("Spicy");

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Failed to remove tag",
    );
    expect(onTagAction).toHaveBeenCalledWith(7, 3, "remove");
    expect(section("Applied Tags").getByText("Spicy")).toBeInTheDocument();
  });

  it("clears the error on the next try, when closed with ×, and when reopened", async () => {
    const onTagAction = jest
      .fn()
      .mockRejectedValueOnce(new Error("Network Error"))
      .mockResolvedValueOnce()
      .mockRejectedValue(new Error("Network Error"));
    const { rerender } = renderDialog(onTagAction);

    // Next try succeeds
    clickTag("Vegan");
    await screen.findByRole("alert");
    clickTag("Vegan");
    await waitFor(() =>
      expect(screen.queryByRole("alert")).not.toBeInTheDocument(),
    );

    // Closed with the alert's ×
    clickTag("Spicy");
    const alert = await screen.findByRole("alert");
    fireEvent.click(within(alert).getByRole("button", { name: "Close" }));
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();

    // Reopened
    clickTag("Spicy");
    await screen.findByRole("alert");
    const props = {
      open: true,
      onClose: jest.fn(),
      selectedItem: ITEM,
      tags: TAGS,
      onTagAction,
    };
    rerender(<TagsDialog {...props} open={false} />);
    rerender(<TagsDialog {...props} />);
    await waitFor(() =>
      expect(screen.queryByRole("alert")).not.toBeInTheDocument(),
    );
  });
});
