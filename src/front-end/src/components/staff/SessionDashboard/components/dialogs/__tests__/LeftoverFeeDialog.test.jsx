import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import LeftoverFeeDialog from "../LeftoverFeeDialog";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), put: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

const LEE = { bill_id: 80, bill_name: "Lee", leftover_charge: 0 };
const PARK = { bill_id: 81, bill_name: "Park", leftover_charge: 0 };
const KIM = { bill_id: 82, bill_name: "Kim", leftover_charge: 0 };

// What GET /Bill/leftover_fee/8 returns: the default, the table's current fee and its open bills
const mockFee = ({
  amount = 0,
  bills = [LEE, PARK, KIM],
  defaultFee = 10,
} = {}) =>
  api.get.mockResolvedValue({
    data: { session_id: 8, default_fee: defaultFee, amount, bills },
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

describe("Leftover fee dialog", () => {
  beforeEach(() => jest.clearAllMocks());

  it("starts on the default fee and previews an even split across the open bills", async () => {
    mockFee();
    renderDialog();

    await waitFor(() => expect(feeInput()).toHaveValue(10));
    expect(api.get).toHaveBeenCalledWith("/Bill/leftover_fee/8");
    expect(screen.getByText("Default $10.00")).toBeInTheDocument();
    expect(
      screen.getByText("Split evenly across 3 open bills"),
    ).toBeInTheDocument();
    // $10.00 / 3: the extra cent goes to the first bill, so the shares add up exactly
    expect(screen.getByText("$3.34")).toBeInTheDocument();
    expect(screen.getAllByText("$3.33")).toHaveLength(2);
    // Nothing to remove yet
    expect(
      screen.queryByRole("button", { name: "Remove fee" }),
    ).not.toBeInTheDocument();
  });

  it("updates the preview as staff change the amount", async () => {
    mockFee({ bills: [LEE, PARK] });
    renderDialog();
    await waitFor(() => expect(feeInput()).toHaveValue(10));

    fireEvent.change(feeInput(), { target: { value: "15" } });

    expect(screen.getAllByText("$7.50")).toHaveLength(2);
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
      expect(api.put).toHaveBeenCalledWith("/Bill/leftover_fee/8", {
        amount: 15,
      }),
    );
    expect(onApplied).toHaveBeenCalledWith(result);
    expect(onClose).toHaveBeenCalled();
  });

  it("starts on the fee already applied, and can remove it", async () => {
    mockFee({
      amount: 15,
      bills: [
        { ...LEE, leftover_charge: 7.5 },
        { ...PARK, leftover_charge: 7.5 },
      ],
    });
    api.put.mockResolvedValue({ data: {} });
    renderDialog();

    await waitFor(() => expect(feeInput()).toHaveValue(15));
    fireEvent.click(screen.getByRole("button", { name: "Remove fee" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith("/Bill/leftover_fee/8", {
        amount: 0,
      }),
    );
  });

  it("can't apply a negative or empty amount", async () => {
    mockFee();
    renderDialog();
    await waitFor(() => expect(feeInput()).toHaveValue(10));

    fireEvent.change(feeInput(), { target: { value: "-5" } });
    expect(screen.getByRole("button", { name: "Apply fee" })).toBeDisabled();

    fireEvent.change(feeInput(), { target: { value: "" } });
    expect(screen.getByRole("button", { name: "Apply fee" })).toBeDisabled();
  });

  it("can't apply a fee when the table has no open bills", async () => {
    mockFee({ bills: [] });
    renderDialog();

    expect(
      await screen.findByText("This table has no open bills."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Apply fee" })).toBeDisabled();
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
