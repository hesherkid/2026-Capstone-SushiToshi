"use client";
import React, { useState } from "react";
import ManagementPage from "@/components/common/ManagementPage";
import { useTags } from "@/hooks/useTags";
import { useTagMenuItems } from "@/hooks/useTagMenuItems";
import { getTagColumns } from "./tagColumns";
import TagFormDialog from "./TagFormDialog";
import DeleteTagDialog from "./DeleteTagDialog";
import TagMenuItemsDialog from "./TagMenuItemsDialog";

const TagManagement = () => {
  const { tags, loading, error, clearError, create, update, remove } =
    useTags();
  const menuItems = useTagMenuItems();
  const [successMessage, setSuccessMessage] = useState(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState("add");
  const [selectedTag, setSelectedTag] = useState(null);
  const [formKey, setFormKey] = useState(0);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [tagToDelete, setTagToDelete] = useState(null);
  const [deleteError, setDeleteError] = useState(null);

  const openForm = (mode, tag = null) => {
    clearError();
    setDialogMode(mode);
    setSelectedTag(tag);
    setFormKey((k) => k + 1);
    setDialogOpen(true);
  };

  const closeForm = () => setDialogOpen(false);

  const handleSave = async (values) => {
    if (dialogMode === "add") {
      await create(values);
      setSuccessMessage("Tag created successfully");
    } else {
      await update(values);
      setSuccessMessage("Tag updated successfully");
    }
    closeForm();
  };

  const openDelete = (tag) => {
    clearError();
    setDeleteError(null);
    setTagToDelete(tag);
    setDeleteDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    try {
      await remove(tagToDelete.tag_id);
      setSuccessMessage("Tag deleted successfully");
      setTagToDelete(null);
    } catch (err) {
      setDeleteError(err?.response?.data?.detail || "Failed to delete tag");
      return false;
    }
  };

  const openItems = (tag) => {
    clearError();
    menuItems.openFor(tag);
  };

  const columns = getTagColumns({
    onEdit: (tag) => openForm("update", tag),
    onViewItems: openItems,
    onDelete: openDelete,
  });

  return (
    <ManagementPage
      title="Tag Management"
      addLabel="Add New Tag"
      onAdd={() => openForm("add")}
      error={error}
      onErrorClose={clearError}
      success={successMessage}
      onSuccessClose={() => setSuccessMessage(null)}
      gridProps={{
        rows: tags,
        columns,
        loading,
        getRowId: (row) => row.tag_id,
      }}
    >
      <TagFormDialog
        key={formKey}
        open={dialogOpen}
        mode={dialogMode}
        tag={selectedTag}
        existingColors={tags.map((t) => t.tag_color)}
        onSave={handleSave}
        onClose={closeForm}
      />

      <DeleteTagDialog
        open={deleteDialogOpen}
        tag={tagToDelete}
        error={deleteError}
        onClose={() => setDeleteDialogOpen(false)}
        onConfirm={handleDeleteConfirm}
      />

      <TagMenuItemsDialog
        open={menuItems.isOpen}
        tag={menuItems.tag}
        items={menuItems.items}
        loading={menuItems.loading}
        loadError={menuItems.loadError}
        error={menuItems.error}
        onErrorClose={menuItems.clearError}
        success={menuItems.success}
        onSuccessClose={menuItems.clearSuccess}
        onClose={menuItems.close}
        onRemoveItem={menuItems.removeItem}
      />
    </ManagementPage>
  );
};

export default TagManagement;
