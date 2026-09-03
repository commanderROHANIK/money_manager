/**
 * Mirrors AddLoanWidget's contract, not AddPropertyWidget's: the parent here refetches the whole
 * list on success rather than rendering field-level server errors, so this widget clears itself
 * on submit rather than keeping what was typed.
 */
import { describe, it, expect } from 'vitest';
import { mount } from '@vue/test-utils';
import AddBankAccountWidget from './AddBankAccountWidget.vue';

const fill = async (wrapper: ReturnType<typeof mount>) => {
  const inputs = wrapper.findAll('input');
  await inputs[0].setValue('Everyday');
  await inputs[1].setValue('OTP Bank');
  await inputs[2].setValue('12345678-00000000');
  await inputs[3].setValue('Checking');
  await inputs[4].setValue('1500');
  await wrapper.find('select').setValue('HUF');
};

describe('AddBankAccountWidget', () => {
  it('emits what was entered', async () => {
    const wrapper = mount(AddBankAccountWidget);

    await fill(wrapper);
    await wrapper.find('form').trigger('submit');

    const created = wrapper.emitted('create');

    expect(created).toHaveLength(1);
    expect(created?.[0][0]).toMatchObject({
      accountName: 'Everyday',
      bankName: 'OTP Bank',
      accountNumber: '12345678-00000000',
      accountType: 'Checking',
      balance: 1500,
      currencyCode: 'HUF',
    });
  });

  it('clears itself after emitting, so the same modal can add a second account', async () => {
    const wrapper = mount(AddBankAccountWidget);

    await fill(wrapper);
    await wrapper.find('form').trigger('submit');

    const inputs = wrapper.findAll('input');

    expect(inputs[0].element.value).toBe('');
    expect(inputs[4].element.value).toBe('0');
    expect(wrapper.find('select').element.value).toBe('EUR');
  });
});
