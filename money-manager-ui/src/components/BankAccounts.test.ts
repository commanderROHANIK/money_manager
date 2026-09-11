/**
 * Behaviours the smoke suite doesn't reach (it mounts every widget once with fixture props and
 * never exercises a handler): money renders in each account's own currency rather than a
 * hardcoded HUF/hu-HU formatter, a failed delete doesn't throw past the click handler, and the
 * modal's form actually creates an account and closes.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import { setLocale } from '../i18n';
import { DEFAULT_LOCALE } from '../i18n/locale';
import BankAccounts from './BankAccounts.vue';
import { bankAccounts, bankBalanceSummary } from '../__tests__/fixtures';

// Chart.js needs a real canvas; jsdom has none. The pie chart is not what this file is about.
vi.mock('vue-chartjs', async () => {
  const { defineComponent, h } = await import('vue');
  return { Pie: defineComponent({ name: 'Pie', render: () => h('canvas') }) };
});

const fetchBankAccounts = vi.fn();
const createBankAccount = vi.fn();
const deleteBankAccount = vi.fn();

vi.mock('../services/api', () => ({
  fetchBankAccounts: () => fetchBankAccounts(),
  fetchBankAccountsTotalBalance: () => Promise.resolve(bankBalanceSummary),
  createBankAccount: (payload: unknown) => createBankAccount(payload),
  deleteBankAccount: (id: number) => deleteBankAccount(id),
}));

beforeEach(() => {
  setLocale('en');
  fetchBankAccounts.mockReset().mockResolvedValue(bankAccounts);
  createBankAccount.mockReset().mockResolvedValue(bankAccounts[0]);
  deleteBankAccount.mockReset().mockResolvedValue(undefined);
});

afterEach(() => setLocale(DEFAULT_LOCALE));

describe('BankAccounts', () => {
  it("formats each account's balance in its own currency rather than a hardcoded one", async () => {
    fetchBankAccounts.mockResolvedValue([
      { id: 9, accountName: 'Dollar account', bankName: 'Chase', accountNumber: '1', accountType: 'Checking', balance: 500, currencyCode: 'USD' },
    ]);

    const wrapper = mount(BankAccounts);
    await flushPromises();

    expect(wrapper.text()).toContain('$500');
    expect(wrapper.text()).not.toContain('Ft');
  });

  it('gives the delete control an accessible name naming the account', async () => {
    const wrapper = mount(BankAccounts);
    await flushPromises();

    expect(wrapper.find(`[aria-label="Remove ${bankAccounts[0].accountName}"]`).exists()).toBe(true);
  });

  it('does not crash the page when a delete fails, and leaves the account listed', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    deleteBankAccount.mockRejectedValue(new Error('conflict'));

    const wrapper = mount(BankAccounts);
    await flushPromises();

    await wrapper.find(`[aria-label="Remove ${bankAccounts[0].accountName}"]`).trigger('click');
    await flushPromises();

    expect(wrapper.text()).toContain(bankAccounts[0].accountName);
  });

  it('adds an account through the modal, refetches, and closes it', async () => {
    const wrapper = mount(BankAccounts);
    await flushPromises();

    const openButton = wrapper.findAll('button').find((b) => b.text() === '+ Add Account');
    await openButton?.trigger('click');

    const inputs = wrapper.findAll('input');
    await inputs[0].setValue('New account');
    await inputs[1].setValue('Bank');
    await inputs[2].setValue('123');
    await inputs[3].setValue('Checking');
    await inputs[4].setValue('100');

    fetchBankAccounts.mockResolvedValue([...bankAccounts, { id: 4, accountName: 'New account', bankName: 'Bank', accountNumber: '123', accountType: 'Checking', balance: 100, currencyCode: 'EUR' }]);

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(createBankAccount).toHaveBeenCalledWith(expect.objectContaining({ accountName: 'New account' }));
    expect(fetchBankAccounts).toHaveBeenCalledTimes(2);
    // The modal closes on success rather than staying open over the freshly reloaded list.
    expect(wrapper.text()).not.toContain('Add a bank account');
  });
});
