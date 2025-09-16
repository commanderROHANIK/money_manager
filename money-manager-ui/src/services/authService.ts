import axios from 'axios';
import type { AxiosResponse } from 'axios';
import router from '../router';

const API_URL = 'http://localhost:5296/api/auth/';

export interface AuthResponse {
  token: string;
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

export function register(
  username: string,
  email: string,
  password: string
): Promise<AxiosResponse<{ message: string }>> {
  const payload: RegisterRequest = { username, email, password };
  return axios.post(API_URL + 'register', payload);
}

export function login(
  username: string,
  password: string
): Promise<AuthResponse> {
  const payload: LoginRequest = { username, password };
  return axios.post<AuthResponse>(API_URL + 'login', payload)
    .then((response) => {
      if (response.data.token) {
        localStorage.setItem('token', response.data.token);
      }
      return response.data;
    });
}

export function logout(): void {
  localStorage.removeItem('token');
  router.push('/login');
  console.log('Logged out, token removed from localStorage');
}

export function getAuthHeader(): { Authorization?: string } {
  const token = localStorage.getItem('token');
  if (token) {
    return { Authorization: `Bearer ${token}` };
  }
  return {};
}

export function isLoggedIn(): boolean {
  return !!localStorage.getItem('token');
}
