import { defaultRowName } from "../RowActions";

describe("defaultRowName", () => {
  it("prefers the row's name fields", () => {
    expect(defaultRowName({ name: "Salmon Roll" })).toBe("Salmon Roll");
    expect(defaultRowName({ tag_name: "Spicy" })).toBe("Spicy");
    expect(defaultRowName({ category_name: "Rolls" })).toBe("Rolls");
  });

  it("falls back to the grid row id", () => {
    expect(defaultRowName({}, { id: 7 })).toBe("#7");
  });

  it("returns an empty string when there is no name or id", () => {
    expect(defaultRowName({}, {})).toBe("");
  });
});
