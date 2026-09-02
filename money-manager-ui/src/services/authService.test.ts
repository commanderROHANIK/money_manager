/**
 * The token lifecycle. Every authenticated request depends on login having stored the token and
 * logout having removed it, and neither was covered.
 *
 * `../router` is mocked because importing the real one pulls in every page component, which is
 * not what these tests are about.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';

const push = vi.fn();
vi.mock('../router', () => ({ default: { push } }));

const post = vi.fn();
const get = vi.fn();
vi.mock('./api', () => ({
  TOKEN_STORAGE_KEY: 'token',
  api: {
    post: (...args: unknown[]) => post(...args),
    get: (...args: unknown[]) => get(...args),
  },
}));

const { login, logout, register, isLoggedIn, fetchCurrentUser, currentUserId } =
  await import('./authService');

/** A syntactically real JWT — three base64url segments — with an arbitrary payload. */
function makeToken(payload: Record<string, unknown>): string {
  const base64url = (obj: Record<string, unknown>) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

  return `${base64url({ alg: 'HS256', typ: 'JWT' })}.${base64url(payload)}.signature`;
}

beforeEach(() => {
  localStorage.clear();
  vi.clearAllMocks();
});

describe('login', () => {
  it('stores the token so later requests carry it', async () => {
    post.mockResolvedValue({ data: { token: 'a-token' } });

    await login('alice', 'password');

    expect(localStorage.getItem('token')).toBe('a-token');
    expect(post).toHaveBeenCalledWith('/auth/login', { username: 'alice', password: 'password' });
  });

  it('stores nothing when the response carries no token', async () => {
    // Defensive: a malformed success response must not write `undefined` into storage, which
    // would make isLoggedIn() true and every subsequent request 401.
    post.mockResolvedValue({ data: {} });

    await login('alice', 'password');

    expect(localStorage.getItem('token')).toBeNull();
  });

  it('leaves any existing token alone when login rejects', async () => {
    localStorage.setItem('token', 'old-token');
    post.mockRejectedValue(new Error('401'));

    await expect(login('alice', 'wrong')).rejects.toThrow();

    expect(localStorage.getItem('token')).toBe('old-token');
  });
});

describe('logout', () => {
  it('clears the token and returns to the login screen', () => {
    localStorage.setItem('token', 'a-token');

    logout();

    expect(localStorage.getItem('token')).toBeNull();
    expect(push).toHaveBeenCalledWith('/login');
  });
});

describe('isLoggedIn', () => {
  it('reflects whether a token is stored', () => {
    expect(isLoggedIn()).toBe(false);

    localStorage.setItem('token', 'a-token');
    expect(isLoggedIn()).toBe(true);
  });
});

describe('register', () => {
  it('posts the credentials without logging the user in', async () => {
    // Registration deliberately does not return a token — the user logs in afterwards.
    post.mockResolvedValue({ data: { message: 'User registered successfully' } });

    await register('alice', 'a@example.com', 'password');

    expect(post).toHaveBeenCalledWith('/auth/register', {
      username: 'alice',
      email: 'a@example.com',
      password: 'password',
    });
    expect(localStorage.getItem('token')).toBeNull();
  });
});

describe('fetchCurrentUser', () => {
  it('returns the user from /auth/me', async () => {
    get.mockResolvedValue({ data: { id: 1, username: 'alice', email: 'a@e.com', baseCurrency: 'HUF' } });

    await expect(fetchCurrentUser()).resolves.toMatchObject({ username: 'alice' });
    expect(get).toHaveBeenCalledWith('/auth/me');
  });
});

describe('currentUserId', () => {
  it('reads the sub claim out of the stored token, without a network call', () => {
    localStorage.setItem('token', makeToken({ sub: '42' }));

    expect(currentUserId()).toBe('42');
    expect(get).not.toHaveBeenCalled();
  });

  it('is null with no token stored', () => {
    expect(currentUserId()).toBeNull();
  });

  it('is null for a malformed token rather than throwing', () => {
    // Whatever namespaces per-device onboarding state by user must not itself crash the
    // dashboard on a corrupted token — it should just read as "no user to namespace by".
    localStorage.setItem('token', 'not-a-jwt');

    expect(currentUserId()).toBeNull();
  });

  it('is null when the payload carries no sub claim', () => {
    localStorage.setItem('token', makeToken({ email: 'a@e.com' }));

    expect(currentUserId()).toBeNull();
  });
});
