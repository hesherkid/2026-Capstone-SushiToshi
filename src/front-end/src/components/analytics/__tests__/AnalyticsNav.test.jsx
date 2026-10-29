import { render, screen } from "@testing-library/react";
import "@testing-library/jest-dom";
import AnalyticsNav, { ANALYTICS_PAGES } from "../AnalyticsNav";
import { usePathname } from "next/navigation";

jest.mock("next/navigation", () => ({
  usePathname: jest.fn(),
}));

jest.mock("next/link", () => {
  return function Link({ children, href, ...props }) {
    return (
      <a href={href} {...props}>
        {children}
      </a>
    );
  };
});

const renderAt = (pathname) => {
  usePathname.mockReturnValue(pathname);
  return render(<AnalyticsNav />);
};

describe("AnalyticsNav", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe("Rendering", () => {
    it("renders a tab for every analytics page", () => {
      renderAt("/analytics/analytic-page");

      const tabs = screen.getAllByRole("tab");
      expect(tabs).toHaveLength(ANALYTICS_PAGES.length);
      expect(tabs.map((t) => t.textContent)).toEqual([
        "Overview",
        "Item performance",
        "Browsing behavior",
        "Order timing",
        "Table turnover",
      ]);
    });

    it.each([
      ["Overview", "/analytics/analytic-page"],
      ["Item performance", "/analytics/item-performance"],
      ["Browsing behavior", "/analytics/browsing-behavior"],
      ["Order timing", "/analytics/order-timing"],
      ["Table turnover", "/analytics/table-turnover"],
    ])("links %s to %s", (label, href) => {
      renderAt("/analytics/analytic-page");

      expect(screen.getByRole("tab", { name: label })).toHaveAttribute(
        "href",
        href,
      );
    });
  });

  describe("Current page", () => {
    it("selects the tab for the current page", () => {
      renderAt("/analytics/item-performance");

      expect(
        screen.getByRole("tab", { name: "Item performance" }),
      ).toHaveAttribute("aria-selected", "true");
      expect(screen.getByRole("tab", { name: "Overview" })).toHaveAttribute(
        "aria-selected",
        "false",
      );
    });

    it("selects Overview on the landing page", () => {
      renderAt("/analytics/analytic-page");

      expect(screen.getByRole("tab", { name: "Overview" })).toHaveAttribute(
        "aria-selected",
        "true",
      );
    });

    it("selects the parent tab on a nested page", () => {
      renderAt("/analytics/order-timing/details");

      expect(screen.getByRole("tab", { name: "Order timing" })).toHaveAttribute(
        "aria-selected",
        "true",
      );
    });

    it("selects no tab on an unknown page", () => {
      renderAt("/analytics/something-else");

      screen.getAllByRole("tab").forEach((tab) => {
        expect(tab).toHaveAttribute("aria-selected", "false");
      });
    });

    it("selects no tab when the pathname isn't available yet", () => {
      renderAt(null);

      screen.getAllByRole("tab").forEach((tab) => {
        expect(tab).toHaveAttribute("aria-selected", "false");
      });
    });
  });

  describe("Accessibility", () => {
    it("is exposed as navigation", () => {
      renderAt("/analytics/analytic-page");

      expect(screen.getByRole("navigation")).toBeInTheDocument();
    });

    it("labels the tab list", () => {
      renderAt("/analytics/analytic-page");

      expect(
        screen.getByRole("tablist", { name: "Analytics pages" }),
      ).toBeInTheDocument();
    });
  });
});
