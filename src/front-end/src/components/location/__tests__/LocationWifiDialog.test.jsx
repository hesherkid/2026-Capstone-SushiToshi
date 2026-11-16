import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import LocationWifiDialog from "../LocationWifiDialog";
import * as wifiCheckService from "@/services/wifiCheckService";

jest.mock("@/services/wifiCheckService");

const location = { location_id: 2, name: "South Side Branch" };
const check = (overrides = {}) => ({
  allowed_networks: [],
  your_address: "203.0.113.10",
  your_network: "203.0.113.10/32",
  can_see_client_addresses: true,
  ...overrides,
});

beforeEach(() => jest.clearAllMocks());

describe("LocationWifiDialog", () => {
  it("shows the check as off when there are no networks", async () => {
    wifiCheckService.getWifiCheck.mockResolvedValue(check());
    render(<LocationWifiDialog open location={location} onClose={jest.fn()} />);

    expect(await screen.findByText("Off: anyone can join")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save" })).toBeDisabled();
  });

  it("adds the admin's own network and saves it", async () => {
    wifiCheckService.getWifiCheck.mockResolvedValue(check());
    wifiCheckService.updateWifiCheck.mockResolvedValue(check({ allowed_networks: ["203.0.113.10/32"] }));
    render(<LocationWifiDialog open location={location} onClose={jest.fn()} />);

    fireEvent.click(await screen.findByRole("button", { name: "Add this network (203.0.113.10/32)" }));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(wifiCheckService.updateWifiCheck).toHaveBeenCalledWith(2, ["203.0.113.10/32"]),
    );
    expect(await screen.findByText("Saved. Guests can only join a table from these networks.")).toBeInTheDocument();
  });

  it("removing every network and saving turns the check off", async () => {
    wifiCheckService.getWifiCheck.mockResolvedValue(check({ allowed_networks: ["192.168.1.0/24"] }));
    wifiCheckService.updateWifiCheck.mockResolvedValue(check());
    render(<LocationWifiDialog open location={location} onClose={jest.fn()} />);

    const chip = await screen.findByRole("button", { name: "Network 192.168.1.0/24" });
    fireEvent.click(within(chip).getByTestId("CancelIcon"));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(wifiCheckService.updateWifiCheck).toHaveBeenCalledWith(2, []));
    expect(await screen.findByText(/The Wi-Fi check is off/)).toBeInTheDocument();
  });

  it("adds a typed network", async () => {
    wifiCheckService.getWifiCheck.mockResolvedValue(check());
    render(<LocationWifiDialog open location={location} onClose={jest.fn()} />);

    fireEvent.change(await screen.findByLabelText("Address or network"), { target: { value: " 192.168.1.0/24 " } });
    fireEvent.click(screen.getByRole("button", { name: "Add" }));

    expect(screen.getByText("192.168.1.0/24")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save" })).toBeEnabled();
  });

  it("shows the server's reason when saving is refused", async () => {
    wifiCheckService.getWifiCheck.mockResolvedValue(check());
    wifiCheckService.updateWifiCheck.mockRejectedValue({
      response: { status: 400, data: { message: "\"wifi\" isn't a valid address or network." } },
    });
    render(<LocationWifiDialog open location={location} onClose={jest.fn()} />);

    fireEvent.change(await screen.findByLabelText("Address or network"), { target: { value: "wifi" } });
    fireEvent.click(screen.getByRole("button", { name: "Add" }));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("\"wifi\" isn't a valid address or network.")).toBeInTheDocument();
  });

  it("warns when the server can't see phones' addresses", async () => {
    wifiCheckService.getWifiCheck.mockResolvedValue(
      check({ your_address: "127.0.0.1", your_network: "127.0.0.1/32", can_see_client_addresses: false }),
    );
    render(<LocationWifiDialog open location={location} onClose={jest.fn()} />);

    expect(await screen.findByText(/coming from 127.0.0.1/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Add this network/ })).not.toBeInTheDocument();
  });
});
