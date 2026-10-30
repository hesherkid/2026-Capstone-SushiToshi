import { Alert, AlertTitle, Box, Chip, Grid, Stack, Typography } from "@mui/material";
import ReportItemsGrid from "@/components/analytics/ReportItemsGrid";
import { describeRange, pct, secs } from "@/components/analytics/reportUtils";
import {
  ALL_ITEMS_COLUMNS,
  AVG_VIEW_COL,
  CONVERSION_COL,
  ItemTable,
  NOW_COL,
  ORDERED_BY_COL,
  RANK_COL,
  UNAVAILABLE_VIEWS_COL,
  VIEWS_COL,
} from "@/components/analytics/browsing-behavior/columns";

// Summary, item finder result and the report tables for one loaded report.
const BrowsingResults = ({
  data,
  loading,
  topN,
  selectedItem,
  selectedItemId,
  onSelect,
}) => {
  const summary = data.summary;
  const tableProps = { range: data.range, selectedItemId, onSelect };

  return (
    <Box sx={{ opacity: loading ? 0.5 : 1 }}>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
        {describeRange(data.range)}. {summary.total_views} views from{" "}
        {summary.viewing_sessions} tables across {summary.items_viewed} of{" "}
        {summary.items_considered} items. Views under{" "}
        {summary.min_view_seconds} seconds aren&apos;t counted (
        {summary.short_views_excluded} left out).
      </Typography>
      <Typography variant="body1" sx={{ mb: 3 }}>
        {pct(summary.conversion_rate)} of item views led to an order at the
        same table. Average view:{" "}
        {secs(summary.average_view_seconds_ordered)} when ordered,{" "}
        {secs(summary.average_view_seconds_not_ordered)} when not.
      </Typography>

      {/* ---------- Item finder result ---------- */}
      {selectedItem && (
        <Alert
          severity={selectedItem.views === 0 ? "warning" : "info"}
          sx={{ mb: 3 }}
          onClose={() => onSelect(null)}
        >
          <AlertTitle>{selectedItem.name}</AlertTitle>
          {selectedItem.views === 0
            ? `Not viewed in this period. ${selectedItem.category_name}.`
            : `Rank #${selectedItem.view_rank} of ${summary.items_viewed} viewed items. ` +
              `${selectedItem.views} views by ${selectedItem.viewing_sessions} tables, ` +
              `${selectedItem.ordering_sessions} of which ordered it (${pct(selectedItem.conversion_rate)}). ` +
              `Average view ${secs(selectedItem.average_view_seconds)}. ` +
              (selectedItem.unavailable_views > 0
                ? `${selectedItem.unavailable_views} views while unavailable. `
                : "") +
              `${selectedItem.category_name}.`}
        </Alert>
      )}

      {/* ---------- Most / least viewed ---------- */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, md: 6 }}>
          <ItemTable
            {...tableProps}
            title="Most viewed"
            items={data.most_viewed}
            columns={[RANK_COL, VIEWS_COL]}
            emptyText="No items were viewed in this period."
          />
        </Grid>
        <Grid size={{ xs: 12, md: 6 }}>
          <ItemTable
            {...tableProps}
            title="Least viewed (excluding zero views)"
            items={data.least_viewed}
            columns={[RANK_COL, VIEWS_COL]}
            emptyText={`Fewer than ${topN * 2} items were viewed, so every viewed item already appears in Most viewed.`}
          />
        </Grid>
      </Grid>

      {/* ---------- Viewed and ordered / not ordered ---------- */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, md: 6 }}>
          <ItemTable
            {...tableProps}
            title={`Top ${topN} viewed and ordered`}
            caption="Tables that viewed the item and ordered it."
            items={data.top_viewed_and_ordered}
            columns={[VIEWS_COL, ORDERED_BY_COL, CONVERSION_COL]}
            emptyText="No table ordered an item it viewed in this period."
          />
        </Grid>
        <Grid size={{ xs: 12, md: 6 }}>
          <ItemTable
            {...tableProps}
            title={`Top ${topN} viewed but not ordered`}
            caption={`${data.viewed_not_ordered.length} items in total were viewed but not ordered.`}
            items={data.top_viewed_not_ordered}
            columns={[VIEWS_COL, AVG_VIEW_COL]}
            emptyText="Every viewed item was ordered by at least one table."
          />
        </Grid>
      </Grid>

      {/* ---------- Low conversion, high views ---------- */}
      <Box sx={{ mb: 3 }}>
        <ItemTable
          {...tableProps}
          title="Low conversion with high views"
          caption={`Items with at least ${summary.min_views} views, lowest conversion first.`}
          items={data.low_conversion_high_views}
          columns={[VIEWS_COL, CONVERSION_COL, AVG_VIEW_COL]}
          emptyText={`No item reached ${summary.min_views} views in this period.`}
        />
      </Box>

      {/* ---------- Never viewed ---------- */}
      {data.never_viewed.length > 0 && (
        <Alert severity="warning" sx={{ mb: 3 }}>
          <AlertTitle>
            {data.never_viewed.length}{" "}
            {data.never_viewed.length === 1 ? "item" : "items"} never viewed
          </AlertTitle>
          <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
            {data.never_viewed.map((item) => (
              <Chip
                key={item.item_id}
                label={
                  item.status === "Seasonal"
                    ? `${item.name} (seasonal)`
                    : item.name
                }
                size="small"
                onClick={() => onSelect(item.item_id)}
              />
            ))}
          </Stack>
        </Alert>
      )}

      {/* ---------- Unavailable views ---------- */}
      <Box sx={{ mb: 3 }}>
        <ItemTable
          {...tableProps}
          title="Views of unavailable items"
          caption="Viewed while the item couldn't be ordered."
          items={data.unavailable_views}
          columns={[UNAVAILABLE_VIEWS_COL, NOW_COL]}
          emptyText="No unavailable items were viewed in this period."
        />
      </Box>

      {/* ---------- All items ---------- */}
      <ReportItemsGrid
        report="browsing-behavior"
        {...tableProps}
        rows={data.items ?? []}
        columns={ALL_ITEMS_COLUMNS}
        highlightRow={(item) => item.views === 0 && item.currently_available}
        emptyText="No items match these filters. Try another category, tag or date range."
      />
    </Box>
  );
};

export default BrowsingResults;
