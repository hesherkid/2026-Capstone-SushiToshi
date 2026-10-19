'use client';

import React, { useState, useEffect } from 'react';
import {
  Box,
  Paper,
  Typography,
  TextField,
  Button,
  Grid,
  Card,
  CardContent,
  CardActions,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Alert,
  Snackbar,
  CircularProgress,
  DialogTitle,
  DialogContent,
  DialogActions,
  Chip,
  Divider,
} from '@mui/material';
import AppDialog from '@/components/common/AppDialog';
import { Download, QrCode, Wifi, Menu } from 'lucide-react';
import { api } from '@/config/api';
import storage from '@/utils/storage';

export default function QRCodeManagement() {
  const [locations, setLocations] = useState([]);
  const [selectedLocation, setSelectedLocation] = useState('');
  const [tableNumber, setTableNumber] = useState('');
  const [tables, setTables] = useState([]);
  const [loading, setLoading] = useState(false);
  const [downloadLoading, setDownloadLoading] = useState(false);
  const [lastGeneratedLocation, setLastGeneratedLocation] = useState(null);
  const [snackbar, setSnackbar] = useState({ open: false, message: '', severity: 'success' });
  const [previewDialog, setPreviewDialog] = useState({ open: false, url: '', title: '', type: '' });

  // Fetch locations on mount
  useEffect(() => {
    fetchLocations();
  }, []);

  // Fetch tables when location changes
  useEffect(() => {
    if (selectedLocation) {
      fetchTables();
    }
  }, [selectedLocation]);

  const fetchLocations = async () => {
    try {
      const response = await api.get('/location');
      setLocations(response.data || []);

      // Get location from storage (set by AppBarWithTitle)
      const storedLocationId = storage.get('branch-location');

      if (storedLocationId && response.data?.some(loc => loc.location_Id === Number(storedLocationId))) {
        // Use stored location if it exists in the list
        setSelectedLocation(Number(storedLocationId));
      } else if (response.data && response.data.length > 0) {
        // Fallback to first location if no stored location
        setSelectedLocation(response.data[0].location_Id);
      }
    } catch (error) {
      console.error('Error fetching locations:', error);
      showSnackbar('Failed to fetch locations', 'error');
    }
  };

  const fetchTables = async () => {
    try {
      const response = await api.get('/TableEntity', {
        params: { locationId: selectedLocation }
      });
      setTables(response.data || []);
    } catch (error) {
      console.error('Error fetching tables:', error);
      showSnackbar('Failed to fetch tables', 'error');
    }
  };

  const showSnackbar = (message, severity = 'success') => {
    setSnackbar({ open: true, message, severity });
  };

  const handleCloseSnackbar = () => {
    setSnackbar({ ...snackbar, open: false });
  };

  const downloadQRCode = async (type) => {
    if (!selectedLocation || !tableNumber) {
      showSnackbar('Please select a location and enter a table number', 'warning');
      return;
    }

    setLoading(true);
    try {
      const endpoint = type === 'wifi' ? '/admin/qr/wifi' : '/admin/qr/session';
      const response = await api.get(endpoint, {
        params: {
          locationId: selectedLocation,
          table: tableNumber,
        },
        responseType: 'blob',
      });

      // Create blob URL and trigger download
      const blob = new Blob([response.data], { type: 'image/png' });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${type}_L${selectedLocation}_T${tableNumber}.png`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      showSnackbar(`${type === 'wifi' ? 'WiFi' : 'Session'} QR code downloaded successfully!`);
    } catch (error) {
      console.error('Error downloading QR code:', error);
      showSnackbar(
        error.response?.data?.message || `Failed to download ${type} QR code`,
        'error'
      );
    } finally {
      setLoading(false);
    }
  };

  const previewQRCode = async (type) => {
    if (!selectedLocation || !tableNumber) {
      showSnackbar('Please select a location and enter a table number', 'warning');
      return;
    }

    setLoading(true);
    try {
      const endpoint = type === 'wifi' ? '/admin/qr/wifi' : '/admin/qr/session';
      const response = await api.get(endpoint, {
        params: {
          locationId: selectedLocation,
          table: tableNumber,
        },
        responseType: 'blob',
      });

      const blob = new Blob([response.data], { type: 'image/png' });
      const url = window.URL.createObjectURL(blob);

      setPreviewDialog({
        open: true,
        url,
        title: `${type === 'wifi' ? 'WiFi' : 'Session'} QR Code - Location ${selectedLocation}, Table ${tableNumber}`,
        type,
      });
    } catch (error) {
      console.error('Error previewing QR code:', error);
      showSnackbar(
        error.response?.data?.message || `Failed to preview ${type} QR code`,
        'error'
      );
    } finally {
      setLoading(false);
    }
  };

  const handleClosePreview = () => {
    if (previewDialog.url) {
      window.URL.revokeObjectURL(previewDialog.url);
    }
    setPreviewDialog({ open: false, url: '', title: '', type: '' });
  };

  const bulkGenerateQRCodes = async () => {
    if (!selectedLocation) {
      showSnackbar('Please select a location', 'warning');
      return;
    }

    if (!tables || tables.length === 0) {
      showSnackbar('No tables found for this location. Please create tables first.', 'warning');
      return;
    }

    setLoading(true);
    try {
      const response = await api.post('/admin/qr/bulk', null, {
        params: {
          locationId: selectedLocation,
        },
      });

      setLastGeneratedLocation(selectedLocation);
      showSnackbar(
        `Successfully generated ${response.data.filesGenerated} QR codes for ${response.data.tableCount} tables!`,
        'success'
      );
    } catch (error) {
      console.error('Error generating bulk QR codes:', error);
      console.error('Error response:', error.response);

      // Extract error message from various possible formats
      let errorMessage = 'Failed to generate bulk QR codes';
      if (error.response?.data) {
        if (typeof error.response.data === 'string') {
          errorMessage = error.response.data;
        } else if (error.response.data.message) {
          errorMessage = error.response.data.message;
        } else if (error.response.data.title) {
          errorMessage = error.response.data.title;
        }
      }

      showSnackbar(errorMessage, 'error');
    } finally {
      setLoading(false);
    }
  };

  const downloadAsZip = async () => {
    if (!lastGeneratedLocation) {
      showSnackbar('Please generate QR codes first', 'warning');
      return;
    }

    setDownloadLoading(true);
    try {
      const response = await api.get('/admin/qr/bulk/download-zip', {
        params: {
          locationId: lastGeneratedLocation,
        },
        responseType: 'blob',
      });

      // Create blob URL and trigger download
      const blob = new Blob([response.data], { type: 'application/zip' });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;

      // Extract filename from Content-Disposition header or use default
      const contentDisposition = response.headers['content-disposition'];
      let fileName = `QRCodes_Location${lastGeneratedLocation}.zip`;
      if (contentDisposition) {
        const fileNameMatch = contentDisposition.match(/filename="?(.+)"?/i);
        if (fileNameMatch) {
          fileName = fileNameMatch[1];
        }
      }

      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      showSnackbar('ZIP file downloaded successfully!');
    } catch (error) {
      console.error('Error downloading ZIP:', error);
      showSnackbar('Failed to download ZIP file', 'error');
    } finally {
      setDownloadLoading(false);
    }
  };

  const downloadAsPdf = async () => {
    if (!lastGeneratedLocation) {
      showSnackbar('Please generate QR codes first', 'warning');
      return;
    }

    setDownloadLoading(true);
    try {
      const response = await api.get('/admin/qr/bulk/download-pdf', {
        params: {
          locationId: lastGeneratedLocation,
        },
        responseType: 'blob',
      });

      // Create blob URL and trigger download
      const blob = new Blob([response.data], { type: 'application/pdf' });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;

      // Extract filename from Content-Disposition header or use default
      const contentDisposition = response.headers['content-disposition'];
      let fileName = `QRCodes_Location${lastGeneratedLocation}.pdf`;
      if (contentDisposition) {
        const fileNameMatch = contentDisposition.match(/filename="?(.+)"?/i);
        if (fileNameMatch) {
          fileName = fileNameMatch[1];
        }
      }

      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      showSnackbar('PDF downloaded successfully!');
    } catch (error) {
      console.error('Error downloading PDF:', error);
      showSnackbar('Failed to download PDF', 'error');
    } finally {
      setDownloadLoading(false);
    }
  };

  const clearCache = async () => {
    if (!selectedLocation || !tableNumber) {
      showSnackbar('Please select a location and enter a table number', 'warning');
      return;
    }

    setLoading(true);
    try {
      await api.delete('/admin/qr/clear', {
        params: {
          locationId: selectedLocation,
          table: tableNumber,
        },
      });

      showSnackbar('QR code cache cleared successfully!');
    } catch (error) {
      console.error('Error clearing cache:', error);
      showSnackbar('Failed to clear QR code cache', 'error');
    } finally {
      setLoading(false);
    }
  };

  const selectedLocationName = locations.find(
    (loc) => loc.location_Id === selectedLocation
  )?.name || '';

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h4" gutterBottom sx={{ mb: 3, fontWeight: 600 }}>
        <QrCode style={{ display: 'inline', marginRight: '10px', verticalAlign: 'middle' }} />
        QR Code Management
      </Typography>

      <Alert severity="info" sx={{ mb: 3 }}>
        Generate QR codes for WiFi access and starting customer dining sessions. QR codes are cached on the
        server for faster access.
      </Alert>

      {/* Individual QR Code Generation - Location and Table Selection */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Individual QR Code Generation
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Select Location and Table
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={12} md={6}>
            <FormControl fullWidth>
              <InputLabel>Location</InputLabel>
              <Select
                value={selectedLocation}
                onChange={(e) => setSelectedLocation(e.target.value)}
                label="Location"
              >
                {locations.map((location) => (
                  <MenuItem key={location.location_Id} value={location.location_Id}>
                    {location.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Grid>
          <Grid item xs={12} md={6}>
            <TextField
              fullWidth
              label="Table Number"
              type="number"
              value={tableNumber}
              onChange={(e) => setTableNumber(e.target.value)}
              inputProps={{ min: 1 }}
            />
          </Grid>
        </Grid>
      </Paper>

      {/* Individual QR Code Generation - Wifi & Session */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid item xs={12} md={12}>
          <Card>
            <CardContent>
              <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
                <Wifi size={32} style={{ marginRight: '10px', color: '#1976d2' }} />
                <Typography variant="h6">WiFi QR Code</Typography>
              </Box>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                Generate a QR code that customers can scan to automatically connect to the
                restaurant WiFi network.
              </Typography>
              <Divider sx={{ my: 2 }} />
              <Typography variant="body2" sx={{ mb: 1 }}>
                <strong>Location:</strong> {selectedLocationName || 'Not selected'}
              </Typography>
              <Typography variant="body2">
                <strong>Table:</strong> {tableNumber || 'Not entered'}
              </Typography>
            </CardContent>
            <CardActions sx={{ justifyContent: 'space-between', px: 2, pb: 2 }}>
              <Button
                variant="outlined"
                startIcon={<QrCode size={18} />}
                onClick={() => previewQRCode('wifi')}
                disabled={loading || !selectedLocation || !tableNumber}
              >
                Preview
              </Button>
              <Button
                variant="contained"
                startIcon={<Download size={18} />}
                onClick={() => downloadQRCode('wifi')}
                disabled={loading || !selectedLocation || !tableNumber}
              >
                Download
              </Button>
            </CardActions>
          </Card>
        </Grid>

        <Grid item xs={12} md={12}>
          <Card>
            <CardContent>
              <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
                <Menu size={32} style={{ marginRight: '10px', color: '#2e7d32' }} />
                <Typography variant="h6">Session QR Code</Typography>
              </Box>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                Generate a QR code that starts a dining session and directs customers to the menu page
                for ordering.
              </Typography>
              <Divider sx={{ my: 2 }} />
              <Typography variant="body2" sx={{ mb: 1 }}>
                <strong>Location:</strong> {selectedLocationName || 'Not selected'}
              </Typography>
              <Typography variant="body2">
                <strong>Table:</strong> {tableNumber || 'Not entered'}
              </Typography>
            </CardContent>
            <CardActions sx={{ justifyContent: 'space-between', px: 2, pb: 2 }}>
              <Button
                variant="outlined"
                startIcon={<QrCode size={18} />}
                onClick={() => previewQRCode('session')}
                disabled={loading || !selectedLocation || !tableNumber}
              >
                Preview
              </Button>
              <Button
                variant="contained"
                startIcon={<Download size={18} />}
                onClick={() => downloadQRCode('session')}
                disabled={loading || !selectedLocation || !tableNumber}
              >
                Download
              </Button>
            </CardActions>
          </Card>
        </Grid>
      </Grid>

      {/* Bulk Generation */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Bulk QR Code Generation
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Generate QR codes for all tables at the selected location. Files will be saved on the server in the
          storage/qrcodes/ directory.
        </Typography>

        {selectedLocation && (
          <Alert severity="info" sx={{ mb: 2 }}>
            {tables.length > 0
              ? `${tables.length} table${tables.length === 1 ? '' : 's'} found at ${selectedLocationName}. This will generate ${tables.length * 2} QR codes (WiFi + Session for each table).`
              : `No tables found at ${selectedLocationName}. Please create tables first.`
            }
          </Alert>
        )}

        <Button
          fullWidth
          variant="contained"
          color="secondary"
          onClick={bulkGenerateQRCodes}
          disabled={loading || !selectedLocation || tables.length === 0}
          startIcon={loading ? <CircularProgress size={20} /> : <QrCode size={20} />}
          sx={{ mb: 2 }}
        >
          {loading ? 'Generating...' : `Generate QR Codes for All ${tables.length} Tables`}
        </Button>

        {lastGeneratedLocation && lastGeneratedLocation === selectedLocation && (
          <>
            <Divider sx={{ my: 2 }} />
            <Typography variant="subtitle2" gutterBottom sx={{ mt: 2 }}>
              Download Generated QR Codes
            </Typography>
            <Alert severity="info" sx={{ mb: 2 }}>
              QR codes are generated at 600×600px. PDF format fits 16 QR codes per page (4 across × 4 down) at 1.75" × 1.75" each, optimized for 8.5" × 11" printing.
            </Alert>
            <Grid container spacing={2}>
              <Grid item xs={12} md={6}>
                <Button
                  fullWidth
                  variant="outlined"
                  startIcon={downloadLoading ? <CircularProgress size={20} /> : <Download size={20} />}
                  onClick={downloadAsZip}
                  disabled={downloadLoading}
                >
                  Download as ZIP (Individual Files)
                </Button>
              </Grid>
              <Grid item xs={12} md={6}>
                <Button
                  fullWidth
                  variant="outlined"
                  startIcon={downloadLoading ? <CircularProgress size={20} /> : <Download size={20} />}
                  onClick={downloadAsPdf}
                  disabled={downloadLoading}
                >
                  Download as PDF (16 QR per page)
                </Button>
              </Grid>
            </Grid>
          </>
        )}
      </Paper>

      {/* Cache Management */}
      <Paper sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Cache Management
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Clear cached QR codes for a specific table. Use this after changing WiFi credentials or
          authentication URLs.
        </Typography>
        <Button
          variant="outlined"
          color="error"
          onClick={clearCache}
          disabled={loading || !selectedLocation || !tableNumber}
        >
          Clear Cache for Table {tableNumber}
        </Button>
      </Paper>

      {/* Preview Dialog */}
      <AppDialog open={previewDialog.open} onClose={handleClosePreview}>
        <DialogTitle>
          {previewDialog.title}
          <Chip
            label={previewDialog.type === 'wifi' ? 'WiFi' : 'Session'}
            size="small"
            color={previewDialog.type === 'wifi' ? 'primary' : 'success'}
            sx={{ ml: 2 }}
          />
        </DialogTitle>
        <DialogContent>
          {previewDialog.url && (
            <Box sx={{ textAlign: 'center', py: 2 }}>
              <img
                src={previewDialog.url}
                alt={previewDialog.title}
                style={{ maxWidth: '100%', height: 'auto' }}
              />
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClosePreview}>Close</Button>
          <Button
            variant="contained"
            startIcon={<Download size={18} />}
            onClick={() => {
              downloadQRCode(previewDialog.type);
              handleClosePreview();
            }}
          >
            Download
          </Button>
        </DialogActions>
      </AppDialog>

      {/* Snackbar for notifications */}
      <Snackbar
        open={snackbar.open}
        autoHideDuration={6000}
        onClose={handleCloseSnackbar}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      >
        <Alert onClose={handleCloseSnackbar} severity={snackbar.severity} sx={{ width: '100%' }}>
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}
