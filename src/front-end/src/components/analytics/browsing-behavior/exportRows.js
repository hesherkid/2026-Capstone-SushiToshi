import { describeRange } from "@/components/analytics/reportUtils";

// Names of the selected category and tag filters, for the top of the CSV.
export const filterNames = (data, categoryId, tagId) => ({
  category:
    data.filters?.categories?.find((c) => String(c.id) === String(categoryId))
      ?.name ?? "All categories",
  tag:
    data.filters?.tags?.find((t) => String(t.tag_id) === String(tagId))?.name ??
    "All tags",
});

// The report's period, location and item filters, then its summary figures.
export const buildSummaryRows = (data, filters) => {
  const summary = data.summary;
  return [
    ["Report", "Browsing behavior"],
    ["Period", describeRange(data.range)],
    ["Location", data.location?.name ?? "All locations"],
    ["Category", filters.category],
    ["Tag", filters.tag],
    ["High views starts at", summary.min_views],
    [],
    ["Measure", "Value"],
    ["Views counted", summary.total_views],
    ["Tables that viewed items", summary.viewing_sessions],
    ["Items viewed", summary.items_viewed],
    ["Items considered", summary.items_considered],
    [
      `Short views left out (under ${summary.min_view_seconds} seconds)`,
      summary.short_views_excluded,
    ],
    ["Conversion (%)", summary.conversion_rate],
    [
      "Average view when ordered (seconds)",
      summary.average_view_seconds_ordered,
    ],
    [
      "Average view when not ordered (seconds)",
      summary.average_view_seconds_not_ordered,
    ],
  ];
};
