import React, { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Stack,
  IconButton,
  DialogContent,
  Divider,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { X } from "lucide-react";
import TagChip from "@/components/tags/TagChip";

const TagsDialog = ({ open, onClose, selectedItem, tags, onTagAction }) => {
  const [selectedTagIds, setSelectedTagIds] = useState(new Set());

  useEffect(() => {
    if (selectedItem?.tags) {
      setSelectedTagIds(new Set(selectedItem.tags.map((t) => t.tag_id)));
    } else {
      setSelectedTagIds(new Set());
    }
  }, [selectedItem, open]);

  const handleTagToggle = async (tagId) => {
    const isSelected = selectedTagIds.has(tagId);
    try {
      await onTagAction(
        selectedItem.item_id,
        tagId,
        isSelected ? "remove" : "add"
      );
      setSelectedTagIds((prev) => {
        const newSet = new Set(prev);
        if (isSelected) {
          newSet.delete(tagId);
        } else {
          newSet.add(tagId);
        }
        return newSet;
      });
    } catch (err) {
      console.error("Failed to update tags:", err);
    }
  };

  const appliedTags = tags.filter((tag) => selectedTagIds.has(tag.tag_id));
  const availableTags = tags.filter((tag) => !selectedTagIds.has(tag.tag_id));

  return (
    <AppDialog open={open} onClose={onClose}>
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          borderBottom: "1px solid",
          borderColor: "divider",
          px: 3,
          py: 2,
        }}
      >
        <Typography
          component="div"
          sx={{
            fontSize: "1.25rem",
            fontWeight: 500,
          }}
        >
          Manage Tags - {selectedItem?.name}
        </Typography>
        <IconButton
          onClick={onClose}
          size="small"
          sx={{
            "&:hover": {
              backgroundColor: "rgba(0, 0, 0, 0.04)",
            },
          }}
        >
          <X size={20} />
        </IconButton>
      </Box>
      <DialogContent sx={{ pt: 2 }}>
        <Typography variant="h6" sx={{ mb: 1 }}>
          Applied Tags
        </Typography>
        <Box
          sx={{
            display: "flex",
            flexWrap: "wrap",
            gap: 1,
          }}
        >
          {appliedTags.map((tag) => (
            <Box key={`tag-${tag.tag_id}`}>
              <TagChip
                tag={tag}
                isActionChip={true}
                isUsedFor="tag-select"
                isSelected={selectedTagIds.has(tag.tag_id)}
                onToggle={handleTagToggle}
              />
            </Box>
          ))}
        </Box>
        <Divider sx={{ my: 2 }} />
        <Typography variant="h6" sx={{ mb: 1 }}>
          Available Tags
        </Typography>

        <Box
          sx={{
            display: "flex",
            flexWrap: "wrap",
            gap: 1,
          }}
        >
          {availableTags.map((tag) => (
            <Box key={`tag-${tag.tag_id}`}>
              <TagChip
                tag={tag}
                isActionChip={true}
                isUsedFor="tag-select"
                isSelected={selectedTagIds.has(tag.tag_id)}
                onToggle={handleTagToggle}
              />
            </Box>
          ))}
        </Box>
      </DialogContent>
    </AppDialog>
  );
};

export default TagsDialog;
