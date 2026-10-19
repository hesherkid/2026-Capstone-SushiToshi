import React from 'react';
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import FullMenu from './page';

jest.mock('@/hooks/useOrderActions', () => ({
  useOrderActions: () => ({
    createOrder: jest.fn(),
    addOrderItems: jest.fn(),
    actionError: null,
    isLoading: false,
    clearActionError: jest.fn(),
  }),
}));

jest.mock('@/components/menu/searchUtils', () => ({
  __esModule: true,
  default: () => ({
    searchTerm: '',
    setSearchTerm: jest.fn(),
    searchResults: [],
  }),
}));

jest.mock('@/config/api', () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
  },
}));

const api = require('@/config/api').default;

describe('FullMenu guest flow', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('guest', 'true');
    localStorage.setItem('session_id', '42');
    // Guests now call the API with their own guest token, through the regular api client
    api.get.mockReset();

    api.get.mockImplementation((url) => {
      // The page asks the server which session the guest is seated in
      if (url === '/DiningSession/participants/active-session-id') {
        return Promise.resolve({ status: 200, data: { session_id: 42 } });
      }

      if (url.includes('/Bill/get_bills/42')) {
        return Promise.resolve({
          status: 200,
          data: [],
        });
      }

      if (url.includes('/DiningSession/session-menu/42')) {
        return Promise.resolve({ status: 200, data: { menu_id: 7, location_Id: 3 } });
      }

      if (url.includes('/Category')) {
        return Promise.resolve({
          status: 200,
          data: [
            { category_id: 1, category_name: 'Appetizers' },
            { category_id: 2, category_name: 'Sushi Rolls' },
          ],
        });
      }

      if (url.includes('/Menu/7/item')) {
        return Promise.resolve({
          status: 200,
          data: [
            {
              item_id: 11,
              name: 'Miso Soup',
              description: 'Classic miso broth',
              price: 6,
              category_id: 1,
              category: 'Appetizers',
              tags: [{ tag_id: 3, name: 'Popular', color_code: '#C01E2E' }],
              item_image_url: '',
            },
            {
              item_id: 12,
              name: 'Salmon Roll',
              description: 'Salmon and rice',
              price: 8,
              category_id: 2,
              category: 'Sushi Rolls',
              tags: [],
              item_image_url: '',
            },
          ],
        });
      }

      return Promise.resolve({ data: [] });
    });
  });

  it('shows the menu items and supports category filtering', async () => {
    render(<FullMenu />);

    await waitFor(() => {
      expect(screen.getByText('Miso Soup')).toBeInTheDocument();
    });

    // Items are requested for the session's location
    expect(api.get).toHaveBeenCalledWith('/Menu/7/item', {
      params: { locationId: 3 },
    });


    expect(
      screen.getByText(/there is no open bill for this session yet/i)
    ).toBeInTheDocument();

    const cartCountHandler = jest.fn();
    window.addEventListener('menu-cart-count-changed', cartCountHandler);

    fireEvent.click(screen.getByRole('button', { name: 'Add one Miso Soup' }));
    await waitFor(() => {
      expect(cartCountHandler).toHaveBeenLastCalledWith(
        expect.objectContaining({ detail: { count: 1 } })
      );
    });
    window.removeEventListener('menu-cart-count-changed', cartCountHandler);

    expect(screen.getByRole('button', { name: 'Full Menu' })).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { name: 'Viewing Full Menu 2 items' })
    ).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Appetizers' }));
    expect(screen.getByRole('heading', { name: 'Appetizers 1 items' })).toBeInTheDocument();
    expect(
      screen.queryByRole('heading', { name: 'Viewing Full Menu 2 items' })
    ).not.toBeInTheDocument();
    expect(screen.queryByText('Salmon Roll')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Full Menu' }));
    expect(screen.getByRole('heading', { name: 'Miso Soup' })).toBeInTheDocument();
    expect(screen.getByText('Salmon Roll')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Viewing Full Menu 2 items' })).toBeInTheDocument();

    fireEvent(window, new Event('menu-cart-open'));
    const cartDrawer = await screen.findByRole('dialog');
    expect(within(cartDrawer).getByRole('heading', { name: 'Your Order' })).toBeInTheDocument();
    expect(within(cartDrawer).getByRole('heading', { name: 'Miso Soup' })).toBeInTheDocument();
    expect(within(cartDrawer).getByText('Qty 1')).toBeInTheDocument();
    expect(within(cartDrawer).queryByRole('button', { name: 'Add one Miso Soup' })).not.toBeInTheDocument();
    expect(within(cartDrawer).getByText('Subtotal')).toBeInTheDocument();
    expect(within(cartDrawer).getByText('$0.00')).toBeInTheDocument();
    expect(
      within(cartDrawer).getByRole('button', { name: 'Place Order (1 item, 1 unit)' })
    ).toBeDisabled();
  });

  it('shows the location price for add-on items', async () => {
    // Wrap the default mock: the item price depends on the locationId sent
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) => {
      if (url.includes('/Menu/7/item')) {
        const atLocation3 = config?.params?.locationId === 3;
        return Promise.resolve({
          status: 200,
          data: [
            {
              item_id: 13,
              name: 'Wagyu Nigiri',
              description: 'Premium add-on',
              price: atLocation3 ? 9 : 6, // location override vs default
              is_add_on: true,            // price is only shown for add-ons
              category_id: 2,
              category: 'Sushi Rolls',
              tags: [],
              item_image_url: '',
            },
          ],
        });
      }
      return baseMock(url, config);
    });

    render(<FullMenu />);

    expect(await screen.findByText('Wagyu Nigiri')).toBeInTheDocument();
    expect(screen.getByText('$9')).toBeInTheDocument();
    expect(screen.queryByText('$6')).not.toBeInTheDocument();
  });

  it('falls back to default prices when the session has no location', async () => {
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) => {
      if (url.includes('/DiningSession/session-menu/42')) {
        return Promise.resolve({ status: 200, data: { menu_id: 7 } }); // no location
      }
      return baseMock(url, config);
    });

    render(<FullMenu />);

    await waitFor(() => {
      expect(screen.getByText('Miso Soup')).toBeInTheDocument();
    });
    expect(api.get).toHaveBeenCalledWith('/Menu/7/item', { params: {} });
  });

  it("tells guests who aren't seated to scan their table's QR code", async () => {
    const seatedMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) =>
      url === '/DiningSession/participants/active-session-id'
        ? Promise.reject({ response: { status: 404 } })
        : seatedMock(url, config)
    );
    render(<FullMenu />);

    expect(
      await screen.findByText(/You're not seated at a table yet/)
    ).toBeInTheDocument();
  });

  it("ignores a stale session_id left in the browser", async () => {
    // Left over from another table; the server says this guest is at session 42
    localStorage.setItem("session_id", "143");
    render(<FullMenu />);

    await waitFor(() =>
      expect(api.get).toHaveBeenCalledWith('/DiningSession/session-menu/42')
    );
    expect(api.get).not.toHaveBeenCalledWith('/DiningSession/session-menu/143');
    expect(localStorage.getItem("session_id")).toBe("42");
  });

  it("stops ordering once the guests have asked for their bill", async () => {
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) =>
      url.includes('/Bill/get_bills/42')
        ? Promise.resolve({
            status: 200,
            data: [{ bill_id: 7, status: 'Open', created_at: '2026-10-18T04:00:00Z', requested_at: '2026-10-18T04:30:00Z' }],
          })
        : baseMock(url, config)
    );
    render(<FullMenu />);

    expect(
      await screen.findAllByText(/You've asked for your bill, so no more orders can be added/)
    ).not.toHaveLength(0);
  });

  it("keeps ordering on the bill this phone used last when the table has several", async () => {
    localStorage.setItem("currentBillId", "8");
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) =>
      url.includes('/Bill/get_bills/42')
        ? Promise.resolve({
            status: 200,
            data: [
              { bill_id: 9, bill_name: 'Park', status: 'Open', created_at: '2026-10-18T04:10:00Z' },
              { bill_id: 8, bill_name: 'Lee', status: 'Open', created_at: '2026-10-18T04:00:00Z' },
              { bill_id: 7, bill_name: 'Old', status: 'Closed', created_at: '2026-10-18T03:00:00Z' },
            ],
          })
        : baseMock(url, config)
    );
    render(<FullMenu />);

    await waitFor(() => expect(localStorage.getItem("currentBillId")).toBe("8"));
    expect(screen.queryByText(/there is no open bill/i)).not.toBeInTheDocument();
  });
});
