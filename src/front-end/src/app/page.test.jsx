import { render, waitFor } from '@testing-library/react';
import HomePage from './page';
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

jest.mock('@/utils/auth', () => ({
  getAuthToken: jest.fn(() => null),
  loginUser: jest.fn(),
}));

describe('HomePage', () => {
  const replace = jest.fn();

  beforeEach(() => {
    replace.mockReset();
    localStorage.clear();
    useRouter.mockReturnValue({ push: jest.fn(), replace });

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
    decodeToken.mockReturnValue({ email: 'guestemail@email.com' });
    defaultUseAuth.mockReturnValue({ isAuthenticated: true, userEmail: 'guestemail@email.com' });
    namedUseAuth.mockReturnValue({ isAuthenticated: true, userEmail: 'guestemail@email.com' });

    render(<HomePage />);

    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith('/menu/full-menu');
    });
  });
});
