import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import PricingProfileForm from "../PricingProfileForm";

const profile = {
  tax_rate: 0.05,
  gratuity_rate: 0.18,
  gratuity_min_party_size: 6,
  leftover_fee: 15,
  weekend_days: ["Friday", "Saturday", "Sunday"],
  apply_holiday_pricing: true,
  rates: [
    { day_type: "Weekday", adult_price: 40.95, senior_price: 35.95, child_price: 25.95, toddler_price: 7.95 },
    { day_type: "Weekend", adult_price: 42.95, senior_price: 39.95, child_price: 28.95, toddler_price: 7.95 },
    { day_type: "Holiday", adult_price: 44.95, senior_price: 39.95, child_price: 28.95, toddler_price: 7.95 },
  ],
};

describe("PricingProfileForm", () => {
  it("shows rates as percentages and the price grid", () => {
    render(<PricingProfileForm profile={profile} onSubmit={jest.fn()} />);

    expect(screen.getByLabelText("GST")).toHaveValue(5);
    expect(screen.getByLabelText("Gratuity")).toHaveValue(18);
    expect(screen.getByLabelText("Weekday Adult price")).toHaveValue(40.95);
    expect(screen.getByLabelText("Holiday Adult price")).toHaveValue(44.95);
    expect(screen.getByLabelText("Fri")).toBeChecked();
    expect(screen.getByLabelText("Mon")).not.toBeChecked();
  });

  it("sends rates back as fractions with all three day types", async () => {
    const onSubmit = jest.fn().mockResolvedValue();
    render(<PricingProfileForm profile={profile} onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText("GST"), { target: { value: "13" } });
    fireEvent.change(screen.getByLabelText("Weekday Adult price"), { target: { value: "45" } });
    fireEvent.click(screen.getByLabelText("Mon"));
    fireEvent.click(screen.getByRole("button", { name: "Save pricing" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    const body = onSubmit.mock.calls[0][0];
    expect(body.tax_rate).toBeCloseTo(0.13);
    expect(body.gratuity_rate).toBeCloseTo(0.18);
    expect(body.weekend_days).toEqual(["Friday", "Saturday", "Sunday", "Monday"]);
    expect(body.rates.map((r) => r.day_type)).toEqual(["Weekday", "Weekend", "Holiday"]);
    expect(body.rates[0]).toMatchObject({ adult_price: 45, toddler_price: 7.95 });
  });

  it("doesn't save a negative price", () => {
    const onSubmit = jest.fn();
    render(<PricingProfileForm profile={profile} onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText("Weekend Child price"), { target: { value: "-1" } });
    fireEvent.click(screen.getByRole("button", { name: "Save pricing" }));

    expect(screen.getByText("Every price must be $0 or more.")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("doesn't save GST over 100%", () => {
    const onSubmit = jest.fn();
    render(<PricingProfileForm profile={profile} onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText("GST"), { target: { value: "150" } });
    fireEvent.click(screen.getByRole("button", { name: "Save pricing" }));

    expect(screen.getByText("GST must be between 0% and 100%.")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("read-only shows the values without a save button", () => {
    render(<PricingProfileForm profile={profile} onSubmit={jest.fn()} readOnly />);

    expect(screen.getByLabelText("Weekday Adult price")).toBeDisabled();
    expect(screen.queryByRole("button", { name: /save/i })).not.toBeInTheDocument();
  });
});
