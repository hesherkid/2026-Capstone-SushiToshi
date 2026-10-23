import React from "react";
import {
  Box,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  DialogTitle,
  DialogContent,
  DialogActions,
  Typography,
  Alert,
  IconButton,
  CircularProgress,
  Tooltip,
} from "@mui/material";
import { Image as ImageIcon, X } from "lucide-react";
import AppDialog from "@/components/common/AppDialog";
import TagChip from "./TagChip";

const TagMenuItemsDialog = ({
  open,
  tag,
  items,
  loading,
  loadError,
  error,
  onErrorClose,
  onClose,
  onRemoveItem,
}) => (
  <AppDialog open={open} onClose={onClose} maxWidth="md">
    <DialogTitle>
      <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
        <Typography>Menu Items with Tag:</Typography>
        {tag && <TagChip tag={tag} size="l" />}
      </Box>
    </DialogTitle>
    <DialogContent dividers>
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={onErrorClose}>
          {error}
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
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell sx={{ color: "white" }}>Image</TableCell>
                <TableCell sx={{ color: "white" }}>Name</TableCell>
                <TableCell sx={{ color: "white" }}>Description</TableCell>
                <TableCell sx={{ color: "white" }} align="center">
                  Actions
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((item) => {
                const imageUrl = item.image_url || item.item_Image_Url;
                return (
                  <TableRow key={item.item_id} hover>
                    <TableCell sx={{ width: 100 }}>
                      {imageUrl ? (
                        <Box
                          component="img"
                          src={imageUrl}
                          alt={item.name}
                          sx={{
                            width: 60,
                            height: 60,
                            objectFit: "cover",
                            borderRadius: 1,
                          }}
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
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography variant="body1">{item.name}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" color="text.secondary">
                        {item.description}
                      </Typography>
                    </TableCell>
                    <TableCell
                      align="right"
                      sx={{ width: "100px", padding: "0 16px" }}
                    >
                      <Box
                        sx={{
                          display: "flex",
                          justifyContent: "center",
                          alignItems: "center",
                          height: "100%",
                        }}
                      >
                        <Tooltip title="Remove tag">
                          <IconButton
                            aria-label={`Remove tag from ${item.name}`}
                            onClick={() => onRemoveItem(item)}
                            color="error"
                            size="small"
                            sx={{
                              "&:hover": {
                                backgroundColor: "error.light",
                                color: "error.dark",
                              },
                            }}
                          >
                            <X size={18} />
                          </IconButton>
                        </Tooltip>
                      </Box>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </DialogContent>
    <DialogActions>
      <Button onClick={onClose}>Close</Button>
    </DialogActions>
  </AppDialog>
);

export default TagMenuItemsDialog;
