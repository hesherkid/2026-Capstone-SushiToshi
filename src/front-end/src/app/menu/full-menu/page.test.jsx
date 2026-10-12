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

jest.mock('@/config/publicApi', () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
  },
}));

const api = require('@/config/api').default;
const publicApi = require('@/config/publicApi').default;

describe('FullMenu guest flow', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('guest', 'true');
    localStorage.setItem('session_id', '42');
    api.get.mockReset();
    publicApi.get.mockReset();

    publicApi.get.mockImplementation((url) => {
      if (url.includes('/Bill/get_bills/42')) {
        return Promise.resolve({
          status: 200,
          data: [],
        });
      }

      if (url.includes('/DiningSession/session-menu/42')) {
        return Promise.resolve({ status: 200, data: { menu_id: 7 } });
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
});
