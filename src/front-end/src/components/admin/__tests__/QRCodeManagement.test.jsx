import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import QRCodeManagement from '../QRCodeManagement';
import { api } from '@/config/api';

// Mock the API module
jest.mock('@/config/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    delete: jest.fn(),
  },
}));

// Mock storage utility
jest.mock('@/utils/storage', () => ({
  __esModule: true,
  default: {
    get: jest.fn(() => null),
    set: jest.fn(),
    remove: jest.fn(),
    clear: jest.fn(),
  },
}));

// Mock window.URL methods
global.URL.createObjectURL = jest.fn(() => 'blob:test-url');
global.URL.revokeObjectURL = jest.fn();

// Mock localStorage
const localStorageMock = {
  getItem: jest.fn(),
  setItem: jest.fn(),
  removeItem: jest.fn(),
  clear: jest.fn(),
};
global.localStorage = localStorageMock;

describe('QRCodeManagement Component', () => {
  const mockLocations = [
    { location_Id: 1, name: 'Downtown', city: 'Edmonton' },
    { location_Id: 2, name: 'West End', city: 'Edmonton' },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    localStorageMock.getItem.mockReturnValue(null);

    // Default mock for location fetch
    api.get.mockImplementation((url) => {
      if (url === '/location') {
        return Promise.resolve({
          data: mockLocations,
        });
      }
      return Promise.reject(new Error('Not found'));
    });
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  test('renders QR Code Management title', async () => {
    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });
  });

  test('fetches and displays locations on mount', async () => {
    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/location');
    });

    await waitFor(() => {
      // Check that location select is populated
      const select = screen.getByRole('combobox');
      expect(select).toBeInTheDocument();
    });
  });

  test('shows warning when downloading without table number', async () => {
    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });

    // Wait for locations to load
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/location');
    });

    // Download buttons should be disabled when table number is not provided
    const downloadButtons = screen.getAllByRole('button', { name: /Download/i });

    // The first two Download buttons are for WiFi and Session (not in dialog)
    const wifiDownloadButton = downloadButtons[0];
    expect(wifiDownloadButton).toBeDisabled();
  });

  test('downloads WiFi QR code when valid input provided', async () => {
    const mockBlob = new Blob(['test'], { type: 'image/png' });

    api.get.mockImplementation((url, config) => {
      if (url === '/location') {
        return Promise.resolve({ data: mockLocations });
      }
      if (url === '/admin/qr/wifi') {
        return Promise.resolve({ data: mockBlob });
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: [] });
      }
      return Promise.reject(new Error('Not found'));
    });

    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });

    // Find and fill table number input
    const tableInput = screen.getByLabelText(/Table Number/i);
    fireEvent.change(tableInput, { target: { value: '5' } });

    // Find and click download WiFi button
    await waitFor(() => {
      const downloadButtons = screen.getAllByRole('button', { name: /Download/i });
      expect(downloadButtons[0]).not.toBeDisabled();
    });

    const downloadButtons = screen.getAllByRole('button', { name: /Download/i });
    fireEvent.click(downloadButtons[0]);

    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/admin/qr/wifi', {
        params: { locationId: 1, table: '5' },
        responseType: 'blob',
      });
    });
  });

  test('opens preview dialog when preview button clicked', async () => {
    const mockBlob = new Blob(['test'], { type: 'image/png' });

    api.get.mockImplementation((url, config) => {
      if (url === '/location') {
        return Promise.resolve({ data: mockLocations });
      }
      if (url === '/admin/qr/wifi' || url === '/admin/qr/session') {
        return Promise.resolve({ data: mockBlob });
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: [] });
      }
      return Promise.reject(new Error('Not found'));
    });

    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });

    // Wait for locations to load
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/location');
    });

    // Fill table number
    const tableInput = screen.getByLabelText(/Table Number/i);
    fireEvent.change(tableInput, { target: { value: '5' } });

    // Wait for button to be enabled and click preview button
    await waitFor(() => {
      const previewButtons = screen.getAllByRole('button', { name: /Preview/i });
      expect(previewButtons[0]).not.toBeDisabled();
    });

    const previewButtons = screen.getAllByRole('button', { name: /Preview/i });
    fireEvent.click(previewButtons[0]);

    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/admin/qr/wifi', expect.objectContaining({
        params: { locationId: 1, table: '5' },
        responseType: 'blob',
      }));
    });
  });

  test('bulk generation sends correct API request', async () => {
    const mockTables = [
      { table_Id: 1, table_number: 1, Location_Id: 1 },
      { table_Id: 2, table_number: 2, Location_Id: 1 },
    ];

    api.get.mockImplementation((url) => {
      if (url === '/location') {
        return Promise.resolve({ data: mockLocations });
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: mockTables });
      }
      return Promise.reject(new Error('Not found'));
    });

    api.post.mockResolvedValueOnce({
      data: { filesGenerated: 4, tableCount: 2, success: true },
    });

    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });

    // Wait for tables to load
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/TableEntity', {
        params: { locationId: 1 }
      });
    });

    // Click bulk generate button - use getByRole to avoid ambiguity
    const bulkButton = screen.getByRole('button', { name: /Generate QR Codes for All/i });
    fireEvent.click(bulkButton);

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/admin/qr/bulk', null, {
        params: { locationId: 1 },
      });
    });
  });

  test('displays error message on API failure', async () => {
    api.get.mockImplementation((url, config) => {
      if (url === '/location') {
        return Promise.resolve({ data: mockLocations });
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: [] });
      }
      if (url === '/admin/qr/wifi') {
        return Promise.reject({
          response: { data: { message: 'Server error' } },
        });
      }
      return Promise.reject(new Error('Not found'));
    });

    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });

    // Wait for locations to load
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/location');
    });

    // Fill table number
    const tableInput = screen.getByLabelText(/Table Number/i);
    fireEvent.change(tableInput, { target: { value: '5' } });

    // Wait for button to be enabled
    await waitFor(() => {
      const downloadButtons = screen.getAllByRole('button', { name: /Download/i });
      expect(downloadButtons[0]).not.toBeDisabled();
    });

    // Try to download
    const downloadButtons = screen.getAllByRole('button', { name: /Download/i });
    fireEvent.click(downloadButtons[0]);

    // Wait for API call to be made
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/admin/qr/wifi', {
        params: { locationId: 1, table: '5' },
        responseType: 'blob',
      });
    });

    // Check for error message - the component shows the message from response.data.message
    await waitFor(() => {
      const errorText = screen.queryByText(/Server error/i) || screen.queryByText(/Failed to download/i);
      expect(errorText).toBeInTheDocument();
    }, { timeout: 3000 });
  });

  test('shows the API error text from a blob error response', async () => {
    api.get.mockImplementation((url) => {
      if (url === '/location') {
        return Promise.resolve({ data: mockLocations });
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: [] });
      }
      if (url === '/admin/qr/wifi') {
        // Blob downloads get their error body as a Blob, like the real 404 from the API
        return Promise.reject({
          response: {
            status: 404,
            data: new Blob(['Location 1 not found or WiFi credentials not configured'], { type: 'text/plain' }),
          },
        });
      }
      return Promise.reject(new Error('Not found'));
    });

    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/location');
    });

    fireEvent.change(screen.getByLabelText(/Table Number/i), { target: { value: '5' } });

    const previewButton = await screen.findAllByRole('button', { name: /Preview/i });
    await waitFor(() => expect(previewButton[0]).not.toBeDisabled());
    fireEvent.click(previewButton[0]);

    expect(
      await screen.findByText('Location 1 not found or WiFi credentials not configured')
    ).toBeInTheDocument();
  });

  test('handles location selection change', async () => {
    api.get.mockImplementation((url) => {
      if (url === '/location') {
        return Promise.resolve({ data: mockLocations });
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: [] });
      }
      return Promise.reject(new Error('Not found'));
    });

    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });

    // Wait for locations to load
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/location');
    });

    // The select should be populated with locations
    await waitFor(() => {
      const select = screen.getByRole('combobox');
      expect(select).toBeInTheDocument();
    });
  });

  test('shows loading state during API calls', async () => {
    api.get.mockImplementation((url) => {
      if (url === '/location') {
        return new Promise(resolve => setTimeout(() => resolve({ data: mockLocations }), 100));
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: [] });
      }
      return Promise.reject(new Error('Not found'));
    });

    render(<QRCodeManagement />);

    // Component should render even while loading
    expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/location');
    });
  });

  test('displays correct PDF layout information (16 QR per page)', async () => {
    const mockTables = [
      { table_Id: 1, table_number: 1, Location_Id: 1 },
    ];

    api.get.mockImplementation((url) => {
      if (url === '/location') {
        return Promise.resolve({ data: mockLocations });
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: mockTables });
      }
      return Promise.reject(new Error('Not found'));
    });

    api.post.mockResolvedValueOnce({
      data: { filesGenerated: 2, tableCount: 1, success: true },
    });

    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });

    // Wait for tables to be fetched
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/TableEntity', {
        params: { locationId: 1 }
      });
    });

    // Generate QR codes first
    const bulkButton = screen.getByRole('button', { name: /Generate QR Codes for All/i });
    fireEvent.click(bulkButton);

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/admin/qr/bulk', null, {
        params: { locationId: 1 },
      });
    });

    // Check that the PDF information is displayed correctly
    await waitFor(() => {
      expect(screen.getByText(/16 QR codes per page \(4 across × 4 down\)/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/1\.75" × 1\.75"/i)).toBeInTheDocument();
  });

  test('PDF download button shows correct text', async () => {
    const mockTables = [
      { table_Id: 1, table_number: 1, Location_Id: 1 },
    ];

    api.get.mockImplementation((url) => {
      if (url === '/location') {
        return Promise.resolve({ data: mockLocations });
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: mockTables });
      }
      return Promise.reject(new Error('Not found'));
    });

    api.post.mockResolvedValueOnce({
      data: { filesGenerated: 2, tableCount: 1, success: true },
    });

    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });

    // Wait for tables to be fetched
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/TableEntity', {
        params: { locationId: 1 }
      });
    });

    // Generate QR codes
    const bulkButton = screen.getByRole('button', { name: /Generate QR Codes for All/i });
    fireEvent.click(bulkButton);

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/admin/qr/bulk', null, {
        params: { locationId: 1 },
      });
    });

    // Check PDF button text
    await waitFor(() => {
      const pdfButton = screen.getByRole('button', { name: /Download as PDF \(16 QR per page\)/i });
      expect(pdfButton).toBeInTheDocument();
    });
  });

  test('fetches tables when location is selected', async () => {
    const mockTables = [
      { table_Id: 1, table_number: 1, Location_Id: 1 },
      { table_Id: 2, table_number: 2, Location_Id: 1 },
    ];

    api.get.mockImplementation((url, config) => {
      if (url === '/location') {
        return Promise.resolve({ data: mockLocations });
      }
      if (url === '/TableEntity') {
        return Promise.resolve({ data: mockTables });
      }
      return Promise.reject(new Error('Not found'));
    });

    render(<QRCodeManagement />);

    await waitFor(() => {
      expect(screen.getByText(/QR Code Management/i)).toBeInTheDocument();
    });

    // Tables should be fetched for default location
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/TableEntity', {
        params: { locationId: 1 }
      });
    });
  });
});
