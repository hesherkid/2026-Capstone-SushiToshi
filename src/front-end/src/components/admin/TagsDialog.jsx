import React, { useEffect, useRef, useState } from "react";
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
  const [focusTagId, setFocusTagId] = useState(null);
  const contentRef = useRef(null);

  useEffect(() => {
    if (selectedItem?.tags) {
      setSelectedTagIds(new Set(selectedItem.tags.map((t) => t.tag_id)));
    } else {
      setSelectedTagIds(new Set());
    }
    setError(null);
  }, [selectedItem, open]);

  useEffect(() => {
    if (focusTagId == null) return;
    contentRef.current
      ?.querySelector(`[data-tag-id="${focusTagId}"] .MuiChip-root`)
      ?.focus();
    setFocusTagId(null);
  }, [focusTagId, selectedTagIds]);

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
      setFocusTagId(tagId);
    } catch (err) {
      console.error("Failed to update tags:", err);
      setError(serverMessage(err, `Failed to ${action} tag`));
    }
  };

  const appliedTags = tags.filter((tag) => selectedTagIds.has(tag.tag_id));
  const availableTags = tags.filter((tag) => !selectedTagIds.has(tag.tag_id));

  const renderTagList = (list, headingId, emptyText) =>
    list.length === 0 ? (
      <Typography variant="body2" color="text.secondary">
        {emptyText}
      </Typography>
    ) : (
      <Box
        role="group"
        aria-labelledby={headingId}
        sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}
      >
        {list.map((tag) => (
          <Box key={`tag-${tag.tag_id}`} data-tag-id={tag.tag_id}>
            <TagChip
              tag={tag}
              isActionChip
              isUsedFor="tag-select"
              isSelected={selectedTagIds.has(tag.tag_id)}
              onToggle={handleTagToggle}
            />
          </Box>
        ))}
      </Box>
    );

  return (
    <AppDialog open={open} onClose={onClose}>
      <DialogHeader
        title="Manage Tags"
        subtitle={selectedItem?.name}
        onClose={onClose}
      />
      <DialogContent dividers ref={contentRef}>
        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        <Typography
          id="applied-tags-heading"
          variant="subtitle1"
          component="h3"
          sx={{ mb: 1 }}
        >
          Applied Tags
        </Typography>
        {renderTagList(
          appliedTags,
          "applied-tags-heading",
          "No tags applied yet",
        )}
        <Divider sx={{ my: 2 }} />
        <Typography
          id="available-tags-heading"
          variant="subtitle1"
          component="h3"
          sx={{ mb: 1 }}
        >
          Available Tags
        </Typography>
        {renderTagList(
          availableTags,
          "available-tags-heading",
          "All tags are applied",
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </AppDialog>
  );
};

export default TagsDialog;
