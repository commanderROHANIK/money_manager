/**
 * Mirrors AddBankAccountWidget's fixture shape, but the contract is different: this widget seeds
 * itself from the account it was handed and keeps the id on submit, since a PUT with no id would
 * have nothing to address.
 */
import { describe, it, expect } from 'vitest';
import { mount } from '@vue/test-utils';
import EditBankAccountWidget from './EditBankAccountWidget.vue';
import type { BankAccount } from '../../../models/models';

const account: BankAccount = {
  id: 7,
  accountName: 'Everyday',
  bankName: 'OTP',
  accountNumber: '12345678-00000000',
  accountType: 'Checking',
  balance: 1500,
  currencyCode: 'HUF',
};

describe('EditBankAccountWidget', () => {
  it('seeds the form from the account it was handed', () => {
    const wrapper = mount(EditBankAccountWidget, { props: { account } });

    const inputs = wrapper.findAll('input');
    expect(inputs[0].element.value).toBe('Everyday');
    expect(inputs[1].element.value).toBe('OTP');
    expect(inputs[2].element.value).toBe('12345678-00000000');
    expect(inputs[3].element.value).toBe('Checking');
    expect(inputs[4].element.value).toBe('1500');
    expect(wrapper.find('select').element.value).toBe('HUF');
  });

  it('emits the edited fields together with the original id', async () => {
    const wrapper = mount(EditBankAccountWidget, { props: { account } });

    const inputs = wrapper.findAll('input');
    await inputs[0].setValue('Everyday checking');
    await inputs[4].setValue('1750');
    await wrapper.find('form').trigger('submit');

    const updated = wrapper.emitted('update');

    expect(updated).toHaveLength(1);
    expect(updated?.[0][0]).toMatchObject({
      id: 7,
      accountName: 'Everyday checking',
      bankName: 'OTP',
      accountNumber: '12345678-00000000',
      accountType: 'Checking',
      balance: 1750,
      currencyCode: 'HUF',
    });
  });

  /**
   * BankAccountsController's BankAccountRequest.Balance is deliberately not restricted to
   * non-negative (an overdraft is ordinary, a credit card balance is negative by definition), so
   * a `min="0"` on this input would let the browser's native constraint validation silently
   * block the one edit that needs to reach a negative balance.
   */
  it('lets the balance go negative, so an overdrawn account can be corrected', async () => {
    const wrapper = mount(EditBankAccountWidget, { props: { account } });

    const balanceInput = wrapper.findAll('input')[4];
    expect(balanceInput.attributes('min')).toBeUndefined();

    await balanceInput.setValue('-250');
    await wrapper.find('form').trigger('submit');

    const updated = wrapper.emitted('update');
    expect(updated?.[0][0]).toMatchObject({ balance: -250 });
  });
});
