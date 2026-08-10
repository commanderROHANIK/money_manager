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
