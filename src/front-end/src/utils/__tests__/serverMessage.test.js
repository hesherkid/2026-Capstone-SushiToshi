import serverMessage from "../serverMessage";

// An axios error with this response body
const errorWith = (data) => ({ response: { status: 400, data } });

describe("serverMessage", () => {
  it("uses a plain string body", () => {
    expect(serverMessage(errorWith("Location to be updated was not found."), "fallback")).toBe(
      "Location to be updated was not found.",
    );
  });

  it("uses { message }", () => {
    expect(serverMessage(errorWith({ message: "Bill is already closed" }), "fallback")).toBe(
      "Bill is already closed",
    );
  });

  it("uses { detail } when there's no message", () => {
    expect(serverMessage(errorWith({ detail: "User ID not found in claims." }), "fallback")).toBe(
      "User ID not found in claims.",
    );
  });

  it("prefers message over detail", () => {
    expect(serverMessage(errorWith({ message: "first", detail: "second" }), "fallback")).toBe("first");
  });

  it("joins ASP.NET validation errors", () => {
    const err = errorWith({
      title: "One or more validation errors occurred.",
      errors: {
        Rates: ["Rates must have exactly one entry each for Weekday, Weekend and Holiday."],
        Tax_Rate: ["The field Tax_Rate must be between 0 and 1."],
      },
    });

    expect(serverMessage(err, "fallback")).toBe(
      "Rates must have exactly one entry each for Weekday, Weekend and Holiday. " +
        "The field Tax_Rate must be between 0 and 1.",
    );
  });

  it("prefers message over validation errors", () => {
    const err = errorWith({ message: "Holiday was not found", errors: { Name: ["Required"] } });

    expect(serverMessage(err, "fallback")).toBe("Holiday was not found");
  });

  it.each([
    ["no response (network error)", { message: "Network Error" }],
    ["an empty string body", errorWith("   ")],
    ["an empty message", errorWith({ message: "" })],
    ["an empty errors object", errorWith({ errors: {} })],
    ["a body with none of the known fields", errorWith({ title: "Bad Request" })],
  ])("falls back for %s", (_, err) => {
    expect(serverMessage(err, "Couldn't save pricing.")).toBe("Couldn't save pricing.");
  });
});
