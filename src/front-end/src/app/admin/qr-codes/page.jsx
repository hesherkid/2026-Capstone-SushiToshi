'use client';

import dynamic from 'next/dynamic';
import { Box, CircularProgress } from '@mui/material';

// Dynamically import the QR code management component
const QRCodeManagement = dynamic(
  () => import('@/components/admin/QRCodeManagement'),
  {
    loading: () => (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="100vh"
      >
        <CircularProgress />
      </Box>
    ),
    ssr: false,
  }
);

export default function QRCodesPage() {
  return (
    <Box sx={{ width: '100%', minHeight: '100vh' }}>
      <QRCodeManagement />
    </Box>
  );
}
