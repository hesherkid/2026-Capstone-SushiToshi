import { render, screen, act } from "@testing-library/react";
import BillsPage from "./page";
import api from "@/config/api";
import { PAGE_LOADING_EVENT } from "@/utils/pageLoading";
import { AUTO_REFRESH_MS } from "@/hooks/useAutoRefresh";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("@/contexts/NotificationContext", () => ({
  useNotification: () => ({ notifySuccess: jest.fn(), notifyError: jest.fn() }),
}));

jest.mock("@/components/staff/SessionDashboard/context/SessionContext", () => ({
  SessionProvider: ({ children }) => children,
}));

jest.mock("@/components/staff/SessionDashboard/components/dialogs/NewBillDialog", () => () => null);
jest.mock("@/components/customer/CustomerBillDialog", () => () => null);

describe("Your Table's Bills page", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    api.get.mockImplementation((url) => {
      if (url === "/DiningSession/participants/active-session-id") return Promise.resolve({ data: { session_id: 42 } });
      if (url === "/Bill/get_bills/42") return Promise.resolve({ status: 200, data: [{ bill_id: 8, bill_name: "Lee", status: "Open", total_count: 2 }] });
      return Promise.resolve({ data: [] });
    });
  });

  it("leaves the loading spinner to the app bar and never flashes 'No bills yet'", async () => {
    const loadingStates = [];
    const record = (event) => loadingStates.push(event.detail.loading);
    window.addEventListener(PAGE_LOADING_EVENT, record);

    render(<BillsPage />);

    expect(screen.getByRole("heading", { name: "Your Table's Bills" })).toBeInTheDocument();
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
    expect(screen.queryByText(/No bills yet/)).not.toBeInTheDocument();

    expect(await screen.findByText("Lee")).toBeInTheDocument();
    expect(screen.queryByText(/No bills yet/)).not.toBeInTheDocument();
    expect(loadingStates[0]).toBe(true);
    expect(loadingStates.at(-1)).toBe(false);
    // Loading stays on from the session check through the bills fetch
    expect(loadingStates.filter((loading) => !loading)).toHaveLength(1);

    window.removeEventListener(PAGE_LOADING_EVENT, record);
  });

  it("tells guests their server opens the bill, then shows it without a reload", async () => {
    jest.useFakeTimers();
    let bills = [];
    api.get.mockImplementation((url) => {
      if (url === "/DiningSession/participants/active-session-id") return Promise.resolve({ data: { session_id: 42 } });
      if (url === "/Bill/get_bills/42") return Promise.resolve({ status: 200, data: bills });
      return Promise.resolve({ data: [] });
    });

    render(<BillsPage />);

    expect(await screen.findByText(/Your server will open a bill for your table/)).toBeInTheDocument();
    expect(screen.queryByText(/Create your first bill/)).not.toBeInTheDocument();

    // Staff open a bill; the next refresh picks it up
    bills = [{ bill_id: 8, bill_name: "Lee", status: "Open", total_count: 2 }];
    await act(async () => jest.advanceTimersByTime(AUTO_REFRESH_MS));

    expect(await screen.findByText("Lee")).toBeInTheDocument();
    jest.useRealTimers();
  });
});
