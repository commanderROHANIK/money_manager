import router from '../router';
import { api, TOKEN_STORAGE_KEY } from './api';
import { clearFeatures } from './features';

export interface AuthResponse {
  token: string;
}

export interface CurrentUser {
  id: number;
  username: string;
  email: string;
  baseCurrency: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
}

export async function register(
  username: string,
  email: string,
  password: string
): Promise<{ message: string }> {
  const payload: RegisterRequest = { username, email, password };
  const response = await api.post<{ message: string }>('/auth/register', payload);
  return response.data;
}

export async function login(username: string, password: string): Promise<AuthResponse> {
  const payload: LoginRequest = { username, password };
  const response = await api.post<AuthResponse>('/auth/login', payload);

  if (response.data.token) {
    localStorage.setItem(TOKEN_STORAGE_KEY, response.data.token);
  }

  return response.data;
}

export async function fetchCurrentUser(): Promise<CurrentUser> {
  const response = await api.get<CurrentUser>('/auth/me');
  return response.data;
}

export function logout(): void {
  localStorage.removeItem(TOKEN_STORAGE_KEY);
  clearFeatures();
  router.push('/login');
}

export function isLoggedIn(): boolean {
  return !!localStorage.getItem(TOKEN_STORAGE_KEY);
}

/**
 * The user id from the stored JWT's `sub` claim (see `TokenProvider` on the API side), decoded
 * client-side without a network round-trip. This is not a trust boundary — nothing
 * security-sensitive is ever decided from it, the server still enforces its own tenant isolation
 * on every request — it exists only to namespace per-device browser state (onboarding's
 * dismissed/declined flags) so one browser profile shared by two accounts on the same machine
 * cannot leak one user's choices into the other's.
 */
export function currentUserId(): string | null {
  try {
    const token = localStorage.getItem(TOKEN_STORAGE_KEY);
    if (!token) return null;

    const payload = token.split('.')[1];
    if (!payload) return null;

    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const decoded: unknown = JSON.parse(atob(base64));

    return typeof decoded === 'object' && decoded !== null && typeof (decoded as { sub?: unknown }).sub === 'string'
      ? (decoded as { sub: string }).sub
      : null;
  } catch {
    // A malformed or absent token should read as "no user to namespace by", not throw.
    return null;
  }
}
