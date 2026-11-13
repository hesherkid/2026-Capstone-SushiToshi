import { createApiWrapper } from "@/utils/apiWrapper";

const makeWrapper = (overrides = {}) => {
  const config = {
    setIsLoading: jest.fn(),
    setActionError: jest.fn(),
    handleError: jest.fn(),
    onSuccess: jest.fn(),
    notifySuccess: jest.fn(),
    ...overrides,
  };
  return { config, apiWrapper: createApiWrapper(config) };
};

describe("createApiWrapper", () => {
  it("shows the success message when the call succeeds", async () => {
    const { config, apiWrapper } = makeWrapper();

    const result = await apiWrapper("createBill", async () => "bill", {
      successMessage: "Bill created successfully",
    });

    expect(result).toBe("bill");
    expect(config.notifySuccess).toHaveBeenCalledWith("Bill created successfully");
  });

  it("doesn't show a success message when the call fails", async () => {
    const { config, apiWrapper } = makeWrapper();

    const result = await apiWrapper(
      "createBill",
      async () => {
        throw new Error("Server down");
      },
      { successMessage: "Bill created successfully" },
    );

    expect(result).toBeNull();
    expect(config.notifySuccess).not.toHaveBeenCalled();
    expect(config.handleError).toHaveBeenCalled();
  });

  it("works without notifySuccess for callers that don't show success messages", async () => {
    const { apiWrapper } = makeWrapper({ notifySuccess: undefined });

    await expect(
      apiWrapper("getOrderById", async () => "order", {
        successMessage: "Loaded",
      }),
    ).resolves.toBe("order");
  });
});
