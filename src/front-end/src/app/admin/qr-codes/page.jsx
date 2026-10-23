'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import dynamic from 'next/dynamic';
import { Box, CircularProgress } from '@mui/material';
import { useAuth } from '@/hooks/useAuth';

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
  const router = useRouter();
  const [isLoading, setIsLoading] = useState(true);
  const [isAuthorized, setIsAuthorized] = useState(false);
  const { isAuthenticated, userRole, loading } = useAuth();

  useEffect(() => {
    const checkAuth = () => {
      try {
        // Wait for auth hook to finish loading
        if (loading) {
          return;
        }

        // Check if user is authenticated via MSAL
        if (!isAuthenticated) {
          router.push('/auth/login');
          return;
        }

        // Admin only, matching the API (AdminQrCodeController)
        if (userRole === 'admin') {
          setIsAuthorized(true);
        } else {
          router.push('/unauthorized');
        }
      } catch (error) {
        console.error('Error verifying authorization:', error);
        router.push('/auth/login');
      } finally {
        setIsLoading(false);
      }
    };

    checkAuth();
  }, [router, isAuthenticated, userRole, loading]);

  if (isLoading || !isAuthorized) {
    return (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="100vh"
      >
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box sx={{ width: '100%', minHeight: '100vh' }}>
      <QRCodeManagement />
    </Box>
  );
}
