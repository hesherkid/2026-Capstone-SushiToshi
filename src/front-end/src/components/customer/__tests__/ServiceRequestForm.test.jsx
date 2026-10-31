import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import ServiceRequestForm from "../OrderDashboard/components/ServiceRequestForm";
import api from "@/config/api";
import { fetchMySessionId } from "@/services/tableSession";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

const mockPush = jest.fn();
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: mockPush }) }));

jest.mock("@/services/tableSession", () => ({
  fetchMySessionId: jest.fn(),
  NOT_SEATED_MESSAGE: "Not seated",
}));

const sendButton = () => screen.getByRole("button", { name: /send request/i });

const renderSeated = async () => {
  render(<ServiceRequestForm />);
  // The session loads first; until then nothing can be sent
  await waitFor(() => expect(api.get).toHaveBeenCalledWith("/ServiceRequest/by-session/8"));
};

const sendWaterRequest = async () => {
  await renderSeated();
  fireEvent.click(screen.getByRole("button", { name: "Water refill" }));
  await waitFor(() => expect(sendButton()).toBeEnabled());
  fireEvent.click(sendButton());
};

describe("Call server form", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    fetchMySessionId.mockResolvedValue(8);
    api.get.mockResolvedValue({ data: [] });
  });

  // Nothing ever saves a table_id, so the request must not depend on one
  it("sends the request with only the session, no saved table needed", async () => {
    api.post.mockResolvedValue({ status: 201, data: {} });

    await sendWaterRequest();

    expect(await screen.findByText("Sent! Your server will be right over.")).toBeInTheDocument();
    expect(api.post).toHaveBeenCalledWith("/ServiceRequest/8", { notes: "Need water refill" });
  });

  it("shows the server's message when it sends back an error object", async () => {
    api.post.mockRejectedValue({ response: { status: 400, data: { message: "Session has ended" } } });

    await sendWaterRequest();

    expect(await screen.findByText("Session has ended")).toBeInTheDocument();
  });

  it("sends several quick requests and a typed note together", async () => {
    api.post.mockResolvedValue({ status: 201, data: {} });
    await renderSeated();

    expect(sendButton()).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Napkins" }));
    fireEvent.click(screen.getByRole("button", { name: "Soy sauce" }));
    expect(screen.getByRole("button", { name: /Napkins/ })).toHaveAttribute("aria-pressed", "true");
    fireEvent.change(screen.getByLabelText(/Anything else/), { target: { value: "Could we get a high chair?" } });
    fireEvent.click(sendButton());

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/ServiceRequest/8", {
        notes: "Need napkins, soy sauce. Could we get a high chair?",
      })
    );
    // Cleared for the next request
    expect(await screen.findByText(/Sent!/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Napkins/ })).toHaveAttribute("aria-pressed", "false");
  });

  it("holds the send button for the cooldown after a request goes out", async () => {
    api.post.mockResolvedValue({ status: 201, data: {} });

    await sendWaterRequest();

    expect(await screen.findByRole("button", { name: "Send another in 30s" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Napkins" }));
    expect(screen.getByRole("button", { name: /Send another in/ })).toBeDisabled();
  });

  it("counts down from the server's time when a request is too soon", async () => {
    api.post.mockRejectedValue({
      response: { status: 429, data: { message: "Your server has your last request.", retry_after_seconds: 42 } },
    });

    await sendWaterRequest();

    expect(await screen.findByText("Your server has your last request.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Send another in 42s" })).toBeDisabled();
    // What they picked stays, ready to send once the wait is over
    expect(screen.getByRole("button", { name: /Water refill/ })).toHaveAttribute("aria-pressed", "true");
  });

  it("unpicks a quick request when it's tapped again", async () => {
    await renderSeated();

    fireEvent.click(screen.getByRole("button", { name: "Wasabi" }));
    fireEvent.click(screen.getByRole("button", { name: /Wasabi/ }));

    expect(screen.getByRole("button", { name: /Wasabi/ })).toHaveAttribute("aria-pressed", "false");
    expect(sendButton()).toBeDisabled();
  });

  it("lists the table's requests still waiting for a server", async () => {
    api.get.mockResolvedValue({
      data: [{ request_id: 5, notes: "Need ginger", status: "Pending", created_at: "2026-10-21T01:30:00" }],
    });
    await renderSeated();

    const waiting = await screen.findByRole("list", { name: "Requests waiting for your server" });
    expect(within(waiting).getByText("Need ginger")).toBeInTheDocument();
  });

  it("points guests to the Bill page to ask for the bill", async () => {
    await renderSeated();

    fireEvent.click(screen.getByRole("button", { name: /Ready to pay/ }));

    expect(mockPush).toHaveBeenCalledWith("/dashboard/bills");
  });

  it("only says so when the guest isn't seated", async () => {
    fetchMySessionId.mockResolvedValue(null);
    render(<ServiceRequestForm />);

    expect(await screen.findByText("Not seated")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /send request/i })).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalled();
  });
});
