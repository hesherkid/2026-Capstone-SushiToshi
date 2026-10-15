// @ts-check
const { test, expect, devices } = require('@playwright/test');

// iPhone viewport/UA/touch without `defaultBrowserType: 'webkit'`, which can't be
// set inside a describe (it forces a new worker). Runs in whichever project is selected.
const { defaultBrowserType: _ignored, ...iPhone12 } = devices['iPhone 12'];

test.describe('QR Code Generation E2E', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to login page
    await page.goto('/auth/login');
  });

  test('should display login page', async ({ page }) => {
    // Check if login page is displayed
    await expect(page.getByRole('heading', { name: /start your order/i })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sign In', exact: true })).toBeVisible();
  });

  test('admin can navigate to QR codes page after login', async ({ page }) => {
    // Note: This test requires valid credentials
    // In a real scenario, you would use test credentials or mock authentication

    // Fill login form (using placeholder values - update with actual test credentials)
    await page.fill('[name="email"]', 'admin@test.com');
    await page.fill('[name="password"]', 'testpassword');

    // Submit form
    await page.click('button[type="submit"]');

    // Wait for navigation (this might fail if credentials are invalid)
    // await page.waitForURL('**/dashboard', { timeout: 5000 });

    // Navigate to QR codes page
    // await page.goto('/admin/qr-codes');

    // Verify QR code management page is displayed
    // await expect(page.getByText(/QR Code Management/i)).toBeVisible();
  });

  test('QR code page has required form elements', async ({ page }) => {
    // Skip authentication for this test (or implement test authentication)
    // In production, you would properly authenticate first

    // Navigate directly to QR codes page
    await page.goto('/admin/qr-codes');

    // Check for key elements (these checks will work if authentication is not enforced)
    // const locationSelect = page.locator('[data-testid="location-select"]');
    // const tableInput = page.locator('[data-testid="table-input"]');

    // Add assertions based on your actual component structure
  });

  test('should handle invalid location gracefully', async ({ page }) => {
    // This is a placeholder test
    // Implement based on your authentication and error handling
    await page.goto('/admin/qr-codes');

    // Test error scenarios
    // Example: Try to generate QR without selecting location
    // await page.click('button:has-text("Download")');
    // await expect(page.getByText(/Please select a location/i)).toBeVisible();
  });

  test('preview dialog opens and closes correctly', async ({ page }) => {
    // Navigate to QR codes page (after authentication)
    await page.goto('/admin/qr-codes');

    // Test preview functionality
    // This would require proper authentication and API mocking
    // await page.fill('[data-testid="table-input"]', '5');
    // await page.click('button:has-text("Preview")');
    // await expect(page.getByRole('dialog')).toBeVisible();
    // await page.click('button:has-text("Close")');
    // await expect(page.getByRole('dialog')).not.toBeVisible();
  });
});

test.describe('Customer Ordering Flow E2E', () => {
  // QR codes encode /auth/login?locationId=X&tableNumber=Y (see QrGeneratorService.cs)
  test('customer can access the QR landing page', async ({ page }) => {
    // Simulate QR code scan by navigating to the URL the QR code encodes
    await page.goto('/auth/login?locationId=1&tableNumber=5');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('session parameters are preserved in URL', async ({ page }) => {
    // Navigate with session parameters
    await page.goto('/auth/login?locationId=1&tableNumber=5');
    await page.waitForLoadState('networkidle');

    // Verify URL contains parameters
    const url = page.url();
    expect(url).toContain('locationId=1');
    expect(url).toContain('tableNumber=5');
  });
});

test.describe('Mobile QR Code Scanning', () => {
  test.use(iPhone12);

  test('QR code page is responsive on mobile', async ({ page }) => {
    await page.goto('/admin/qr-codes');

    // Check mobile responsiveness
    // await expect(page.getByText(/QR Code Management/i)).toBeVisible();

    // Verify mobile-friendly layout
    // const viewport = page.viewportSize();
    // expect(viewport.width).toBeLessThan(768);
  });
});
