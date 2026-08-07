/**
 * Covers the two interceptors in api.ts, which are the only place auth is applied to outgoing
 * requests and the only place an expired session is handled.
 *
 * Both were previously untested, and both have a failure mode that is invisible in the UI: a
 * request interceptor that stops attaching the token makes every call 401, and a response
 * interceptor that clears the token on the wrong status logs the user out at random.
 *
 * Requests are intercepted by swapping axios's adapter rather than mocking axios itself, so the
 * real interceptor chain runs.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import type { InternalAxiosRequestConfig } from 'axios';
import { api, TOKEN_STORAGE_KEY } from './api';

const originalAdapter = api.defaults.adapter;
let assign: ReturnType<typeof vi.fn>;

/** Records the config the interceptor chain produced, then answers with `status`. */
function respondWith(status: number) {
  const seen: { config?: InternalAxiosRequestConfig } = {};

  api.defaults.adapter = async (config) => {
    seen.config = config as InternalAxiosRequestConfig;

    if (status >= 200 && status < 300) {
      return { data: {}, status, statusText: 'OK', headers: {}, config };
    }

    // A custom adapter is responsible for rejecting non-2xx itself; axios only applies
    // validateStatus inside its own built-in adapters.
    return Promise.reject(Object.assign(new Error(`Request failed with status ${status}`), {
      config,
      response: { data: {}, status, statusText: '', headers: {}, config },
    }));
  };

  return seen;
}

beforeEach(() => {
  localStorage.clear();

  assign = vi.fn();
  // jsdom's real location throws "Not implemented: navigation" on assign().
  Object.defineProperty(window, 'location', {
    value: { pathname: '/', assign },
    writable: true,
    configurable: true,
  });
});

afterEach(() => {
  api.defaults.adapter = originalAdapter;
  vi.restoreAllMocks();
});

describe('request interceptor', () => {
  it('attaches the stored token as a bearer credential', () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'a-token');
    const seen = respondWith(200);

    return api.get('/RentalProperties').then(() => {
      expect(seen.config?.headers.Authorization).toBe('Bearer a-token');
    });
  });

  it('sends no Authorization header when there is no token', () => {
    const seen = respondWith(200);

    return api.get('/RentalProperties').then(() => {
      expect(seen.config?.headers.Authorization).toBeUndefined();
    });
  });
});

describe('response interceptor', () => {
  it('clears the token and returns to login on 401', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'expired-token');
    respondWith(401);

    await expect(api.get('/RentalProperties')).rejects.toThrow();

    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull();
    expect(assign).toHaveBeenCalledWith('/login');
  });

  it('does not navigate when the 401 arrives while already on the login screen', async () => {
    // Without this guard a failed login request would navigate to /login from /login, which
    // reloads the page and throws away the error the user needs to see.
    window.location.pathname = '/login';
    localStorage.setItem(TOKEN_STORAGE_KEY, 'expired-token');
    respondWith(401);

    await expect(api.post('/auth/login')).rejects.toThrow();

    expect(assign).not.toHaveBeenCalled();
  });

  it('leaves the session alone when the server errors for some other reason', async () => {
    // A 500 is the server's problem, not the session's. Logging the user out here would turn a
    // transient backend fault into a lost session.
    localStorage.setItem(TOKEN_STORAGE_KEY, 'a-token');
    respondWith(500);

    await expect(api.get('/RentalProperties')).rejects.toThrow();

    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBe('a-token');
    expect(assign).not.toHaveBeenCalled();
  });

  it('passes a successful response through untouched', async () => {
    respondWith(200);

    const response = await api.get('/RentalProperties');

    expect(response.status).toBe(200);
  });
});
