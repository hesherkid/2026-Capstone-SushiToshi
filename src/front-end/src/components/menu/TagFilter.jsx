import { Box, ButtonBase } from "@mui/material";
import MaterialSymbol from "@/components/common/MaterialSymbol";

// Same keys as components/Tags, so a tag reads the same however the API spelled it
export const tagKey = (tag) => String(tag.tag_id ?? tag.Tag_Id ?? tag.name ?? tag.tag_name);
const tagName = (tag) => tag.name ?? tag.tag_name ?? "";

// Every tag used on the menu, once each, A to Z
export const menuTags = (items) => {
  const byKey = new Map();
  items.forEach((item) =>
    (item.tags ?? []).forEach((tag) => {
      if (tagName(tag)) byKey.set(tagKey(tag), { key: tagKey(tag), name: tagName(tag) });
    }),
  );
  return [...byKey.values()].sort((a, b) => a.name.localeCompare(b.name));
};

// Whether the item has every picked tag
export const hasAllTags = (item, selectedKeys) =>
  selectedKeys.every((key) => (item.tags ?? []).some((tag) => tagKey(tag) === key));

// Sideways-scrolling row of pills under the search box; tap to filter by a tag.
// A pill with a `symbol` (Favourites, Order again) shows that icon, filled once picked
const TagFilter = ({ tags, selected, onChange }) => {
  if (!tags.length) return null;

  const toggle = (key) =>
    onChange(selected.includes(key) ? selected.filter((k) => k !== key) : [...selected, key]);

  const pill = {
    flexShrink: 0,
    height: 32,
    px: 1.5,
    gap: 0.5,
    borderRadius: 999,
    fontSize: "0.85rem",
    whiteSpace: "nowrap",
    transition: "background-color 150ms ease, color 150ms ease",
    "&.Mui-focusVisible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: 2 },
  };

  return (
    <Box
      role="group"
      aria-label="Filter the menu"
      sx={{
        display: "flex",
        alignItems: "center",
        gap: 1,
        overflowX: "auto",
        scrollbarWidth: "none",
        "&::-webkit-scrollbar": { display: "none" },
      }}
    >
      <MaterialSymbol name="filter_list" size={20} sx={{ color: "text.secondary", flexShrink: 0 }} />
      {tags.map(({ key, name, symbol }) => {
        const isSelected = selected.includes(key);
        return (
          <ButtonBase
            key={key}
            aria-pressed={isSelected}
            onClick={() => toggle(key)}
            sx={{
              ...pill,
              fontWeight: isSelected ? 700 : 500,
              bgcolor: isSelected ? "primary.main" : "grey.100",
              color: isSelected ? "common.white" : "grey.800",
              "&:hover": { bgcolor: isSelected ? "primary.dark" : "grey.200" },
            }}
          >
            {symbol ? (
              <MaterialSymbol name={symbol} filled={isSelected} size={16} />
            ) : (
              isSelected && <MaterialSymbol name="check" size={16} />
            )}
            {name}
          </ButtonBase>
        );
      })}
      {selected.length > 0 && (
        <ButtonBase
          aria-label="Clear filters"
          onClick={() => onChange([])}
          sx={{ ...pill, fontWeight: 500, color: "primary.main", "&:hover": { bgcolor: "grey.100" } }}
        >
          Clear
        </ButtonBase>
      )}
    </Box>
  );
};

export default TagFilter;
