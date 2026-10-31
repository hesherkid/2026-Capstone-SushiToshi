import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import HomePage from './page';
import CustomerHome from '@/components/home/CustomerHome';
import { useRouter } from 'next/navigation';
import useAuth from '@/hooks/useAuth';

jest.mock('next/navigation', () => ({
  useRouter: jest.fn(),
  usePathname: jest.fn(() => '/'),
}));

jest.mock('@/hooks/useAuth', () => ({
  __esModule: true,
  default: jest.fn(),
  useAuth: jest.fn(),
}));

jest.mock('@/config/api', () => ({
  api: {
    get: jest.fn().mockResolvedValue({ data: [] }),
  },
}));

jest.mock('@/config/auth', () => ({
  decodeToken: jest.fn(),
  isTokenExpired: jest.fn(() => false),
}));

jest.mock('@/services/tableSession', () => ({
  fetchMySessionId: jest.fn(),
}));

jest.mock('@/utils/auth', () => ({
  getAuthToken: jest.fn(() => null),
  loginUser: jest.fn(),
  logoutCustomer: jest.fn(),
  isGuestClaims: jest.requireActual('@/utils/auth').isGuestClaims,
}));

describe('HomePage', () => {
  const replace = jest.fn();
  const push = jest.fn();

  beforeEach(() => {
    replace.mockReset();
    localStorage.clear();
    // Earlier tests sign in as a guest; start each one signed out
    require('@/utils/auth').getAuthToken.mockReturnValue(null);
    require('@/config/auth').decodeToken.mockReset();
    require('@/services/tableSession').fetchMySessionId.mockReset();
    push.mockReset();
    useRouter.mockReturnValue({ push, replace });

    const { default: defaultUseAuth, useAuth: namedUseAuth } = require('@/hooks/useAuth');
    defaultUseAuth.mockReturnValue({ isAuthenticated: false, userEmail: null });
    namedUseAuth.mockReturnValue({ isAuthenticated: false, userEmail: null });
  });

  it('redirects guests flagged in localStorage to the menu page', async () => {
    localStorage.setItem('guest', 'true');

    render(<HomePage />);

    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith('/menu/full-menu');
    });
  });

  it('redirects a signed-in guest account to the menu page', async () => {
    const { getAuthToken } = require('@/utils/auth');
    const { decodeToken } = require('@/config/auth');
    const { default: defaultUseAuth, useAuth: namedUseAuth } = require('@/hooks/useAuth');

    getAuthToken.mockReturnValue('token');
    decodeToken.mockReturnValue({ email: 'guest-3f9c2a@guest.sushitoshi.invalid' });
    defaultUseAuth.mockReturnValue({ isAuthenticated: true, userEmail: 'guest-3f9c2a@guest.sushitoshi.invalid' });
    namedUseAuth.mockReturnValue({ isAuthenticated: true, userEmail: 'guest-3f9c2a@guest.sushitoshi.invalid' });

    render(<HomePage />);

    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith('/menu/full-menu');
    });
  });

  describe('registered customer', () => {
    const signIn = () => {
      const { default: defaultUseAuth, useAuth: namedUseAuth } = require('@/hooks/useAuth');
      const auth = { isAuthenticated: true, userRole: 'customer', userEmail: 'sam@example.com', loading: false };
      defaultUseAuth.mockReturnValue(auth);
      namedUseAuth.mockReturnValue(auth);
    };

    // Signing in never joins a table; only scanning its QR code does
    it('stays on the home page even when the server has them in an open session', async () => {
      signIn();

      render(<HomePage />);

      expect(await screen.findByRole('heading', { name: 'Welcome back!' })).toBeInTheDocument();
      expect(replace).not.toHaveBeenCalled();
      expect(require('@/services/tableSession').fetchMySessionId).not.toHaveBeenCalled();
    });

    it('shows how to get started, greeting them by name if known', async () => {
      localStorage.setItem('user', JSON.stringify({ first_name: 'Sam' }));
      signIn();

      render(<HomePage />);

      expect(await screen.findByRole('heading', { name: 'Welcome back, Sam!' })).toBeInTheDocument();
      expect(screen.getByText(/Scan your table's QR code/)).toBeInTheDocument();
      expect(replace).not.toHaveBeenCalled();
    });

    it('links to their past visits', async () => {
      signIn();

      render(<HomePage />);
      fireEvent.click(await screen.findByRole('button', { name: /Past visits/ }));

      expect(push).toHaveBeenCalledWith('/visits');
    });

    it('lets them log out between visits, clearing the last visit too', async () => {
      const { logoutCustomer } = require('@/utils/auth');
      signIn();

      render(<HomePage />);
      fireEvent.click(await screen.findByRole('button', { name: 'Log out' }));

      expect(logoutCustomer).toHaveBeenCalled();
    });

    it("won't log them out while seated at a table, and takes them back to it instead", async () => {
      localStorage.setItem('session_id', '42');
      localStorage.setItem('tableNumber', '8');
      signIn();

      render(<HomePage />);

      expect(await screen.findByText("You're seated at Table 8")).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Log out' })).not.toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'Back to your table' }));
      expect(push).toHaveBeenCalledWith('/menu/full-menu');
    });

    it('checks right away whether staff have closed the table, so log out comes back sooner', async () => {
      localStorage.setItem('session_id', '42');
      signIn();

      render(<HomePage />);

      await screen.findByText("You're seated at a table");
      await waitFor(() =>
        expect(require('@/services/tableSession').fetchMySessionId).toHaveBeenCalled(),
      );
    });
  });

  // Each useAuth call starts out signed out while it reads the token. Acting on that sent
  // customers who had just signed in straight back to the login page
  it('waits for the sign-in check before deciding, so a signed-in customer stays', async () => {
    const { default: defaultUseAuth } = require('@/hooks/useAuth');
    defaultUseAuth.mockReturnValue({ isAuthenticated: false, loading: true });

    const { rerender } = render(<CustomerHome />);
    expect(replace).not.toHaveBeenCalled();

    defaultUseAuth.mockReturnValue({ isAuthenticated: true, userRole: 'customer', loading: false });
    rerender(<CustomerHome />);

    expect(await screen.findByRole('heading', { name: 'Welcome back!' })).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });

  it('sends signed-out visitors to sign in first', async () => {
    render(<HomePage />);

    await waitFor(() => expect(replace).toHaveBeenCalledWith('/auth/login'));
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });
});
