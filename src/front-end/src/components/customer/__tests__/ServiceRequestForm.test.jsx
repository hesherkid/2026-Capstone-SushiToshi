import { act, render, screen, waitFor, fireEvent } from "@testing-library/react";
import ServiceRequestForm from "../OrderDashboard/components/ServiceRequestForm";
import api from "@/config/api";
import { fetchMySessionId } from "@/services/tableSession";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

const mockPush = jest.fn();
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: mockPush }) }));

// The real dialog is covered by its own tests; here we only care which bill it opens
jest.mock("@/components/customer/CustomerBillDialog", () => ({
  __esModule: true,
  default: ({ open, billId }) => (open ? <div>Bill dialog for {billId}</div> : null),
}));

jest.mock("@/services/tableSession", () => ({
  fetchMySessionId: jest.fn(),
  NOT_SEATED_MESSAGE: "Not seated",
}));

const sendWaterRequest = async () => {
  render(<ServiceRequestForm />);
  const send = screen.getByRole("button", { name: /send request/i });
  fireEvent.click(screen.getByRole("button", { name: "Need water refill" }));
  await waitFor(() => expect(send).toBeEnabled());
  fireEvent.click(send);
};

describe("Call server form", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    fetchMySessionId.mockResolvedValue(8);
  });

  // Nothing ever saves a table_id, so the request must not depend on one
  it("sends the request with only the session, no saved table needed", async () => {
    api.post.mockResolvedValue({ status: 201, data: {} });

    await sendWaterRequest();

    expect(await screen.findByText("Request submitted successfully!")).toBeInTheDocument();
    expect(api.post).toHaveBeenCalledWith("/ServiceRequest/8", { notes: "Need water refill" });
  });

  it("shows the server's message when it sends back an error object", async () => {
    api.post.mockRejectedValue({ response: { status: 400, data: { message: "Session has ended" } } });

    await sendWaterRequest();

    expect(await screen.findByText("Session has ended")).toBeInTheDocument();
  });

  describe("Ready for bill", () => {
    const bills = (...list) => api.get.mockResolvedValue({ data: list });
    const tapReadyForBill = async () => {
      render(<ServiceRequestForm />);
      // Let the session load first, or the tap is ignored
      await act(async () => {});
      fireEvent.click(screen.getByRole("button", { name: "Ready for bill" }));
    };

    it("opens the bill summary for the table's only open bill instead of sending a note", async () => {
      bills({ bill_id: 80, status: "Open" }, { bill_id: 79, status: "Closed" });

      await tapReadyForBill();

      expect(await screen.findByText("Bill dialog for 80")).toBeInTheDocument();
      expect(api.get).toHaveBeenCalledWith("/Bill/get_bills/8");
      expect(api.post).not.toHaveBeenCalled();
    });

    it("opens the bill this phone last ordered on when the table has several", async () => {
      localStorage.setItem("currentBillId", "81");
      bills({ bill_id: 80, status: "Open" }, { bill_id: 81, status: "Open" });

      await tapReadyForBill();

      expect(await screen.findByText("Bill dialog for 81")).toBeInTheDocument();
    });

    it("sends guests to Bill Summary to pick when it can't tell which bill is theirs", async () => {
      bills({ bill_id: 80, status: "Open" }, { bill_id: 81, status: "Open" });

      await tapReadyForBill();

      await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/dashboard/bills"));
    });

    it("explains when the table has no open bill yet", async () => {
      bills();

      await tapReadyForBill();

      expect(await screen.findByText(/doesn't have an open bill yet/)).toBeInTheDocument();
    });
  });
});
