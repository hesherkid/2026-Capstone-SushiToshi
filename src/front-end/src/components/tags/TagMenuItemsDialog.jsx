import React from "react";
import {
  Box,
  Button,
  DialogContent,
  DialogActions,
  Typography,
  Alert,
  CircularProgress,
} from "@mui/material";
import { Image as ImageIcon, X } from "lucide-react";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import ManagementGrid from "@/components/common/ManagementGrid";
import { actionsColumn } from "@/components/common/RowActions";
import TagChip from "./TagChip";

const ItemImage = ({ item }) => {
  const imageUrl = item.image_url || item.item_Image_Url;
  return imageUrl ? (
    <Box
      component="img"
      src={imageUrl}
      alt={item.name}
      sx={{ width: 60, height: 60, objectFit: "cover", borderRadius: 1 }}
    />
  ) : (
    <Box
      sx={{
        width: 60,
        height: 60,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        bgcolor: "grey.100",
        borderRadius: 1,
      }}
    >
      <ImageIcon size={24} color="grey" />
    </Box>
  );
};

const getTagItemColumns = ({ onRemoveItem }) => [
  {
    field: "image",
    headerName: "Image",
    width: 90,
    sortable: false,
    filterable: false,
    renderCell: ({ row }) => <ItemImage item={row} />,
  },
  { field: "name", headerName: "Name", flex: 1, minWidth: 140 },
  {
    field: "description",
    headerName: "Description",
    flex: 2,
    minWidth: 200,
    // Wraps instead of cutting off; the row grows to fit (see getRowHeight below)
    renderCell: ({ value }) => (
      <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: "normal", py: 1 }}>
        {value}
      </Typography>
    ),
  },
  actionsColumn(
    (row) => [
      { label: `Remove tag from ${row.name}`, icon: X, color: "error", onClick: () => onRemoveItem(row) },
    ],
    { width: 90 },
  ),
];

const TagMenuItemsDialog = ({
  open,
  tag,
  items,
  loading,
  loadError,
  error,
  onErrorClose,
  success,
  onSuccessClose,
  onClose,
  onRemoveItem,
}) => (
  <AppDialog open={open} onClose={onClose} maxWidth="md">
    <DialogHeader
      title="Menu Items with Tag"
      subtitle={tag && <TagChip tag={tag} size="l" />}
      onClose={onClose}
    />
    <DialogContent dividers>
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={onErrorClose}>
          {error}
        </Alert>
      )}
      {success && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={onSuccessClose}>
          {success}
        </Alert>
      )}
      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : loadError ? (
        <Alert severity="error" sx={{ mb: 2 }}>
          {loadError}
        </Alert>
      ) : items.length === 0 ? (
        <Typography color="text.secondary" align="center" sx={{ py: 3 }}>
          No menu items found with this tag
        </Typography>
      ) : (
        <ManagementGrid
          rows={items}
          columns={getTagItemColumns({ onRemoveItem })}
          getRowId={(row) => row.item_id}
          // Taller rows for the photos and wrapped descriptions
          getRowHeight={() => "auto"}
          showToolbar={false}
          hideFooter={items.length <= 10}
          disableColumnMenu
          sx={{ "& .MuiDataGrid-cell": { minHeight: 76 } }}
        />
      )}
    </DialogContent>
    <DialogActions>
      <Button onClick={onClose}>Close</Button>
    </DialogActions>
  </AppDialog>
);

export default TagMenuItemsDialog;
