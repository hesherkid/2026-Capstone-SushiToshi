import React, { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Button,
  DialogActions,
  DialogContent,
  Divider,
  Alert,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import TagChip from "@/components/tags/TagChip";
import { serverMessage } from "@/utils/serverMessage";

const TagsDialog = ({ open, onClose, selectedItem, tags, onTagAction }) => {
  const [selectedTagIds, setSelectedTagIds] = useState(new Set());
  const [error, setError] = useState(null);

  useEffect(() => {
    if (selectedItem?.tags) {
      setSelectedTagIds(new Set(selectedItem.tags.map((t) => t.tag_id)));
    } else {
      setSelectedTagIds(new Set());
    }
    setError(null);
  }, [selectedItem, open]);

  const handleTagToggle = async (tagId) => {
    const isSelected = selectedTagIds.has(tagId);
    const action = isSelected ? "remove" : "add";
    setError(null);
    try {
      await onTagAction(selectedItem.item_id, tagId, action);
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
      setError(serverMessage(err, `Failed to ${action} tag`));
    }
  };

  const appliedTags = tags.filter((tag) => selectedTagIds.has(tag.tag_id));
  const availableTags = tags.filter((tag) => !selectedTagIds.has(tag.tag_id));

  return (
    <AppDialog open={open} onClose={onClose}>
      <DialogHeader title="Manage Tags" subtitle={selectedItem?.name} onClose={onClose} />
      <DialogContent dividers>
        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        <Typography variant="subtitle1" sx={{ mb: 1 }}>
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
        <Typography variant="subtitle1" sx={{ mb: 1 }}>
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
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </AppDialog>
  );
};

export default TagsDialog;
