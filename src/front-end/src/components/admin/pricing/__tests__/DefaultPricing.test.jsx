import { render, screen } from "@testing-library/react";
import DefaultPricing from "../DefaultPricing";
import * as pricingService from "@/services/pricingService";

jest.mock("@/services/pricingService");

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

const profile = {
  tax_rate: 0.05,
  gratuity_rate: 0.18,
  gratuity_min_party_size: 6,
  leftover_fee: 15,
  weekend_days: ["Saturday", "Sunday"],
  apply_holiday_pricing: true,
  updated_at: "2026-10-01T00:00:00Z",
  rates: [],
};

beforeEach(() => {
  jest.clearAllMocks();
  pricingService.getDefaultPricing.mockResolvedValue(profile);
});

describe("DefaultPricing", () => {
  it("lists the locations with their own pricing as chips", async () => {
    pricingService.getLocationPricingList.mockResolvedValue([
      { location_id: 1, location_name: "Downtown", source: "Location" },
      { location_id: 2, location_name: "Southgate", source: "Default" },
      { location_id: 3, location_name: "West End", source: "Location" },
    ]);
    render(<DefaultPricing />);

    expect(await screen.findByText("Locations with their own pricing:")).toBeInTheDocument();
    expect(screen.getByText("Downtown").closest(".MuiChip-root")).toBeInTheDocument();
    expect(screen.getByText("West End").closest(".MuiChip-root")).toBeInTheDocument();
    expect(screen.queryByText("Southgate")).not.toBeInTheDocument();
  });

  it("leaves the row out when every location uses the default", async () => {
    pricingService.getLocationPricingList.mockResolvedValue([
      { location_id: 2, location_name: "Southgate", source: "Default" },
    ]);
    render(<DefaultPricing />);

    expect(await screen.findByLabelText("GST")).toBeInTheDocument();
    expect(screen.queryByText("Locations with their own pricing:")).not.toBeInTheDocument();
  });
});
