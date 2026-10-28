import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import LocationPricingDialog from "../LocationPricingDialog";
import * as pricingService from "@/services/pricingService";

jest.mock("@mui/x-data-grid", () => {
  if (typeof global.TextEncoder === "undefined") {
    const { TextEncoder, TextDecoder } = jest.requireActual("util");
    Object.assign(global, { TextEncoder, TextDecoder });
  }
  const actual = jest.requireActual("@mui/x-data-grid");
  const React = jest.requireActual("react");
  return {
    ...actual,
    DataGrid: (props) => React.createElement(actual.DataGrid, { ...props, disableVirtualization: true }),
  };
});

if (typeof window.ResizeObserver === "undefined") {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}


jest.mock("@/services/pricingService");
jest.mock("next/link", () => ({
  __esModule: true,
  default: ({ href, children }) => <a href={href}>{children}</a>,
}));

const location = { location_Id: 1, name: "Downtown" };
const profile = (source) => ({
  source,
  updated_at: "2026-10-24T00:00:00Z",
  tax_rate: 0.05, gratuity_rate: 0.18, gratuity_min_party_size: 6, leftover_fee: 15,
  weekend_days: ["Friday", "Saturday", "Sunday"], apply_holiday_pricing: true,
  rates: ["Weekday", "Weekend", "Holiday"].map((day_type) => ({
    day_type, adult_price: 40.95, senior_price: 35.95, child_price: 25.95, toddler_price: 7.95,
  })),
});

beforeEach(() => jest.clearAllMocks());

describe("LocationPricingDialog", () => {
  it("shows the default pricing read-only until custom pricing is switched on", async () => {
    pricingService.getLocationPricing.mockResolvedValue(profile("Default"));
    render(<LocationPricingDialog open location={location} onClose={jest.fn()} />);

    expect(await screen.findByLabelText("Weekday Adult price")).toBeDisabled();
    expect(screen.getByLabelText("Custom pricing for this location")).not.toBeChecked();
    expect(screen.queryByRole("button", { name: "Save location pricing" })).not.toBeInTheDocument();
  });

  it("switching on and saving gives the location its own pricing", async () => {
    pricingService.getLocationPricing.mockResolvedValue(profile("Default"));
    pricingService.updateLocationPricing.mockResolvedValue(profile("Location"));
    render(<LocationPricingDialog open location={location} onClose={jest.fn()} />);
    await screen.findByLabelText("Weekday Adult price");

    fireEvent.click(screen.getByLabelText("Custom pricing for this location"));
    // Saving the default prices unchanged is allowed: switching on is the change
    expect(screen.getByRole("button", { name: "Save location pricing" })).toBeEnabled();
    fireEvent.change(screen.getByLabelText("Weekday Adult price"), { target: { value: "45" } });
    fireEvent.click(screen.getByRole("button", { name: "Save location pricing" }));

    await waitFor(() =>
      expect(pricingService.updateLocationPricing).toHaveBeenCalledWith(
        1,
        expect.objectContaining({
          rates: expect.arrayContaining([expect.objectContaining({ day_type: "Weekday", adult_price: 45 })]),
        }),
      ),
    );
    expect(await screen.findByText(/now uses its own pricing/)).toBeInTheDocument();
  });

  it("switching off saved custom pricing asks first, then goes back to the default", async () => {
    pricingService.getLocationPricing.mockResolvedValue(profile("Location"));
    pricingService.resetLocationPricing.mockResolvedValue(profile("Default"));
    render(<LocationPricingDialog open location={location} onClose={jest.fn()} />);
    await screen.findByLabelText("Weekday Adult price");

    fireEvent.click(screen.getByLabelText("Custom pricing for this location"));
    fireEvent.click(await screen.findByRole("button", { name: "Use default pricing" }));

    await waitFor(() => expect(pricingService.resetLocationPricing).toHaveBeenCalledWith(1));
    expect(await screen.findByText(/uses the default pricing again/)).toBeInTheDocument();
  });
});
