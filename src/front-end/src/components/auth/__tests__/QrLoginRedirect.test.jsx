import { render, waitFor } from "@testing-library/react";
import { useRouter, useSearchParams } from "next/navigation";
import JoinPage from "@/app/join/page";
import StartSessionPage from "@/app/start-session/page";

jest.mock("next/navigation", () => ({
  useRouter: jest.fn(),
  useSearchParams: jest.fn(),
}));

describe("Legacy QR links", () => {
  const replace = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    useRouter.mockReturnValue({ replace });
  });

  it("sends /join?table=&location= to the login page", async () => {
    useSearchParams.mockReturnValue(new URLSearchParams("table=4&location=1"));
    render(<JoinPage />);

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith("/auth/login?tableNumber=4&locationId=1")
    );
  });

  it("sends /start-session?tableNumber=&locationId= to the login page", async () => {
    useSearchParams.mockReturnValue(new URLSearchParams("locationId=2&tableNumber=9"));
    render(<StartSessionPage />);

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith("/auth/login?tableNumber=9&locationId=2")
    );
  });

  it("goes to plain login when the link has no table details", async () => {
    useSearchParams.mockReturnValue(new URLSearchParams());
    render(<JoinPage />);

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/auth/login"));
  });
});
