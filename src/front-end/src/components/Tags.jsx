import { useMemo } from "react";
import { Box } from "@mui/material";

// Tags on the customer menu cards: plain grey pills, the same for every tag.
// (Admin screens use TagChip, which shows each tag's own colour.)
const Tags = ({ tags = [] }) => {
  const normalized = useMemo(() => {
    return (Array.isArray(tags) ? tags : []).map((t, idx) => ({
      tag_id: t.tag_id ?? t.Tag_Id ?? t.id ?? `${t.name ?? t.tag_name ?? idx}`,
      name: t.name ?? t.tag_name ?? "",
    }));
  }, [tags]);

  if (!normalized.length) return null;

  return (
    <Box
      sx={{
        display: "flex",
        flexWrap: "wrap",
        gap: 0.5,
        mt: 0.75,
        maxWidth: "100%",
      }}
    >
      {normalized.map((tag) => (
        <Box
          key={tag.tag_id}
          component="span"
          sx={{
            px: 1.25,
            py: 0.25,
            borderRadius: 999,
            bgcolor: "grey.100",
            color: "grey.800",
            fontSize: "0.72rem",
            fontWeight: 500,
            lineHeight: 1.5,
            whiteSpace: "nowrap",
          }}
        >
          {tag.name}
        </Box>
      ))}
    </Box>
  );
};

export default Tags;
