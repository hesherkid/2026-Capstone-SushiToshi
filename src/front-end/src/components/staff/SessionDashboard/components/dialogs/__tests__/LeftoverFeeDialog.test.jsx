import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import LeftoverFeeDialog, { PREVIEW_DELAY_MS } from "../LeftoverFeeDialog";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), put: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

const LEE = {
  bill_id: 80,
  bill_name: "Lee",
  guest_count: 2,
  leftover_charge: 0,
};
const PARK = {
  bill_id: 81,
  bill_name: "Park",
  guest_count: 1,
  leftover_charge: 0,
};
const KIM = {
  bill_id: 82,
  bill_name: "Kim",
  guest_count: 1,
  leftover_charge: 0,
};

const URL = "/Bill/leftover-fee/8";

// GET /Bill/leftover-fee/8 returns the default, the table's current fee and its open bills.
// With ?amount=, it returns each bill's share of that amount.
const mockFee = ({
  amount = 0,
  bills = [LEE, PARK, KIM],
  defaultFee = 10,
  previews = {},
} = {}) =>
  api.get.mockImplementation((url, config) => {
    const previewed = config?.params?.amount;
    if (previewed === undefined) {
      return Promise.resolve({
        data: { session_id: 8, default_fee: defaultFee, amount, bills },
      });
    }
    const shares = previews[previewed] ?? bills.map(() => 0);
    return Promise.resolve({
      data: {
        session_id: 8,
        default_fee: defaultFee,
        amount: previewed,
        bills: bills.map((b, i) => ({ ...b, leftover_charge: shares[i] })),
      },
    });
  });

const renderDialog = (props = {}) => {
  const onClose = jest.fn();
  const onApplied = jest.fn();
  render(
    <LeftoverFeeDialog
      open
      sessionId={8}
      onClose={onClose}
      onApplied={onApplied}
      {...props}
    />,
  );
  return { onClose, onApplied };
};

const feeInput = () => screen.getByLabelText("Fee for the table");

const previewCallsFor = (value) =>
  api.get.mock.calls.filter(([, config]) => config?.params?.amount === value);

const waitPastPreviewDelay = () =>
  new Promise((resolve) => setTimeout(resolve, PREVIEW_DELAY_MS + 100));

describe("Leftover fee dialog", () => {
  beforeEach(() => jest.clearAllMocks());

  it("starts on the default fee and shows the split by guests", async () => {
    mockFee({ previews: { 10: [5, 2.5, 2.5] } });

    renderDialog();

    await waitFor(() => expect(feeInput()).toHaveValue(10));
    expect(api.get).toHaveBeenCalledWith(URL);
    expect(screen.getByText("Default $10.00")).toBeInTheDocument();
    expect(
      screen.getByText("Shared by guests across 3 open bills"),
    ).toBeInTheDocument();
    expect(screen.getByText("(2 guests)")).toBeInTheDocument();
    expect(await screen.findByText("$5.00")).toBeInTheDocument();
    expect(screen.getAllByText("$2.50")).toHaveLength(2);
    expect(api.get).toHaveBeenCalledWith(URL, { params: { amount: 10 } });
    expect(
      screen.queryByRole("button", { name: "Remove fee" }),
    ).not.toBeInTheDocument();
  });

  it("shows exactly what the server returns, without working it out itself", async () => {
    mockFee({ previews: { 10: [6.01, 2.99, 1] } });

    renderDialog();

    expect(await screen.findByText("$6.01")).toBeInTheDocument();
    expect(screen.getByText("$2.99")).toBeInTheDocument();
    expect(screen.getByText("$1.00")).toBeInTheDocument();
  });

  it("asks the server again as staff change the amount", async () => {
    mockFee({
      bills: [LEE, PARK],
      previews: { 10: [6.67, 3.33], 15: [10, 5] },
    });
    renderDialog();
    expect(await screen.findByText("$6.67")).toBeInTheDocument();

    fireEvent.change(feeInput(), { target: { value: "15" } });

    expect(await screen.findByText("$10.00")).toBeInTheDocument();
    expect(screen.getByText("$5.00")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith(URL, { params: { amount: 15 } });
  });

  it("waits for staff to stop typing before asking", async () => {
    mockFee({ bills: [LEE, PARK] });
    renderDialog();
    await waitFor(() => expect(previewCallsFor(10)).toHaveLength(1));

    fireEvent.change(feeInput(), { target: { value: "1" } });
    fireEvent.change(feeInput(), { target: { value: "12" } });
    fireEvent.change(feeInput(), { target: { value: "125" } });
    await waitPastPreviewDelay();

    expect(previewCallsFor(1)).toHaveLength(0);
    expect(previewCallsFor(12)).toHaveLength(0);
    expect(previewCallsFor(125)).toHaveLength(1);
  });

  it("doesn't preview a negative or empty amount, and can't apply it", async () => {
    mockFee();
    renderDialog();
    await waitFor(() => expect(feeInput()).toHaveValue(10));

    fireEvent.change(feeInput(), { target: { value: "-5" } });
    await waitPastPreviewDelay();

    expect(previewCallsFor(-5)).toHaveLength(0);
    expect(screen.getByRole("button", { name: "Apply fee" })).toBeDisabled();

    fireEvent.change(feeInput(), { target: { value: "" } });

    expect(screen.getByRole("button", { name: "Apply fee" })).toBeDisabled();
  });

  it("applies the amount to the table, then tells the bill and closes", async () => {
    mockFee({ bills: [LEE, PARK] });
    const result = { session_id: 8, default_fee: 10, amount: 15, bills: [] };
    api.put.mockResolvedValue({ data: result });
    const { onClose, onApplied } = renderDialog();
    await waitFor(() => expect(feeInput()).toHaveValue(10));

    fireEvent.change(feeInput(), { target: { value: "15" } });
    fireEvent.click(screen.getByRole("button", { name: "Apply fee" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(URL, { amount: 15 }),
    );
    expect(onApplied).toHaveBeenCalledWith(result);
    expect(onClose).toHaveBeenCalled();
  });

  it("starts on the fee already applied, and can remove it", async () => {
    mockFee({
      amount: 15,
      bills: [
        { ...LEE, leftover_charge: 10 },
        { ...PARK, leftover_charge: 5 },
      ],
    });
    api.put.mockResolvedValue({ data: {} });
    renderDialog();

    await waitFor(() => expect(feeInput()).toHaveValue(15));
    fireEvent.click(screen.getByRole("button", { name: "Remove fee" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(URL, { amount: 0 }),
    );
  });

  it("can't apply a fee when the table has no open bills, and doesn't preview one", async () => {
    mockFee({ bills: [] });
    renderDialog();

    expect(
      await screen.findByText("This table has no open bills."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Apply fee" })).toBeDisabled();
    await waitPastPreviewDelay();
    expect(previewCallsFor(10)).toHaveLength(0);
  });

  it("stays open and explains when the fee can't be applied", async () => {
    mockFee();
    api.put.mockRejectedValue({
      response: {
        data: {
          message: "This table has no open bills to add a leftover fee to.",
        },
      },
    });
    const { onClose, onApplied } = renderDialog();
    await waitFor(() => expect(feeInput()).toHaveValue(10));

    fireEvent.click(screen.getByRole("button", { name: "Apply fee" }));

    expect(
      await screen.findByText(
        "This table has no open bills to add a leftover fee to.",
      ),
    ).toBeInTheDocument();
    expect(onApplied).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });
});
