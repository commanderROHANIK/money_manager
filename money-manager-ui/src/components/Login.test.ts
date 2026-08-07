/**
 * The two auth screens, which were untested despite being the only way into the app.
 *
 * These exist mainly as a safety net for converting both files to TypeScript — they were the
 * last plain-JS components in the tree, so nothing type-checked them. Written first, so the
 * conversion has something to prove it preserved behaviour.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import type { Component } from 'vue';

const login = vi.fn();
const register = vi.fn();
vi.mock('../services/authService', () => ({
  login: (...a: unknown[]) => login(...a),
  register: (...a: unknown[]) => register(...a),
}));

const push = vi.fn();
const reload = vi.fn();

import Login from './Login.vue';
import Register from './Register.vue';

// Typed as Component rather than `typeof Login`: now that both files use defineComponent they
// have distinct precise instance types, so a concrete annotation would only fit one of them.
const mountWith = (component: Component) =>
  mount(component, { global: { mocks: { $router: { push } } } });

beforeEach(() => {
  vi.clearAllMocks();
  Object.defineProperty(window, 'location', {
    value: { pathname: '/login', reload, assign: vi.fn() },
    writable: true,
    configurable: true,
  });
});

describe('Login', () => {
  const fill = async (wrapper: ReturnType<typeof mountWith>, user: string, pass: string) => {
    const inputs = wrapper.findAll('input');
    await inputs[0].setValue(user);
    await inputs[1].setValue(pass);
  };

  it('submits the entered credentials', async () => {
    login.mockResolvedValue({ token: 't' });
    const wrapper = mountWith(Login);

    await fill(wrapper, 'alice', 'password');
    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(login).toHaveBeenCalledWith('alice', 'password');
  });

  it('lands on the dashboard after a successful login', async () => {
    login.mockResolvedValue({ token: 't' });
    const wrapper = mountWith(Login);

    await fill(wrapper, 'alice', 'password');
    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(push).toHaveBeenCalledWith('/');
  });

  it('shows an error and stays put when the credentials are rejected', async () => {
    login.mockRejectedValue(new Error('401'));
    const wrapper = mountWith(Login);

    await fill(wrapper, 'alice', 'wrong');
    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(wrapper.text()).toContain('Invalid login');
    expect(push).not.toHaveBeenCalled();
  });

  it('shows no error before anything is submitted', () => {
    expect(mountWith(Login).text()).not.toContain('Invalid login');
  });

  it('masks the password field', () => {
    expect(mountWith(Login).findAll('input')[1].attributes('type')).toBe('password');
  });
});

describe('Register', () => {
  const fill = async (
    wrapper: ReturnType<typeof mountWith>,
    values: [string, string, string, string]
  ) => {
    const inputs = wrapper.findAll('input');
    for (let i = 0; i < values.length; i++) await inputs[i].setValue(values[i]);
  };

  it('refuses to submit when the two passwords differ', async () => {
    // Caught client-side: the API has no confirm-password concept, so nothing else would.
    const wrapper = mountWith(Register);

    await fill(wrapper, ['alice', 'a@e.com', 'password', 'different']);
    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(register).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain('Passwords do not match');
  });

  it('submits and reports success', async () => {
    register.mockResolvedValue({ message: 'ok' });
    const wrapper = mountWith(Register);

    await fill(wrapper, ['alice', 'a@e.com', 'password', 'password']);
    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(register).toHaveBeenCalledWith('alice', 'a@e.com', 'password');
    expect(wrapper.text()).toContain('Registered successfully');
  });

  it('clears the form after a successful registration', async () => {
    register.mockResolvedValue({ message: 'ok' });
    const wrapper = mountWith(Register);

    await fill(wrapper, ['alice', 'a@e.com', 'password', 'password']);
    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(wrapper.findAll('input').map((i) => i.element.value)).toEqual(['', '', '', '']);
  });

  it('reports a failure without clearing what was typed', async () => {
    register.mockRejectedValue(new Error('That username is already registered'));
    const wrapper = mountWith(Register);

    await fill(wrapper, ['alice', 'a@e.com', 'password', 'password']);
    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(wrapper.text()).toContain('Error registering');
    expect(wrapper.findAll('input')[0].element.value).toBe('alice');
  });

  it('re-enables the submit button after a failure', async () => {
    // The loading flag is cleared in a finally block; without it a failed attempt would leave
    // the form permanently disabled.
    register.mockRejectedValue(new Error('nope'));
    const wrapper = mountWith(Register);

    await fill(wrapper, ['alice', 'a@e.com', 'password', 'password']);
    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeUndefined();
  });
});
