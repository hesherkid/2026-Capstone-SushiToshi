import React from "react";
import { Box, Typography } from "@mui/material";
import { Edit, Trash2, Eye } from "lucide-react";
import { actionsColumn } from "@/components/common/RowActions";
import TagChip from "./TagChip";

export const getTagColumns = ({ onEdit, onViewItems, onDelete }) => [
  {
    field: "tag_name",
    headerName: "Preview",
    flex: 0.25,
    sortable: true,
    renderCell: (params) => {
      if (!params?.row) return null;
      return (
        <Box sx={{ display: "flex", justifyContent: "left", width: "100%" }}>
          <TagChip tag={params.row} size="xl" />
        </Box>
      );
    },
  },
  {
    field: "tag_color",
    headerName: "Color Code",
    flex: 0.25,
    renderCell: (params) => {
      if (!params?.value) {
        return <Typography>N/A</Typography>;
      }
      return (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Box
            sx={{
              width: 24,
              height: 24,
              bgcolor: params.value,
              borderRadius: 1,
              border: "1px solid rgba(0, 0, 0, 0.1)",
            }}
          />
          <Typography>{params.value}</Typography>
        </Box>
      );
    },
  },
  actionsColumn(
    (tag) => [
      { label: "Edit", icon: Edit, onClick: () => onEdit(tag) },
      { label: "View Items", icon: Eye, onClick: () => onViewItems(tag) },
      {
        label: "Delete",
        icon: Trash2,
        color: "error",
        onClick: () => onDelete(tag),
      },
    ],
    { flex: 0.25 },
  ),
];

export default getTagColumns;
