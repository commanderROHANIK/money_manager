/**
 * Same contract as AddLoanWidget and AddBankAccountWidget: the parent refetches on success, so
 * this widget clears itself on submit rather than keeping what was typed.
 */
import { describe, it, expect } from 'vitest';
import { mount } from '@vue/test-utils';
import AddStockWidget from './AddStockWidget.vue';

const fill = async (wrapper: ReturnType<typeof mount>) => {
  const inputs = wrapper.findAll('input');
  await inputs[0].setValue('MSFT');
  await inputs[1].setValue('10');
  await inputs[2].setValue('300');
  await inputs[3].setValue('350');
  await inputs[4].setValue('2026-01-15');
  await wrapper.find('select').setValue('USD');
};

describe('AddStockWidget', () => {
  it('emits what was entered', async () => {
    const wrapper = mount(AddStockWidget);

    await fill(wrapper);
    await wrapper.find('form').trigger('submit');

    const created = wrapper.emitted('create');

    expect(created).toHaveLength(1);
    expect(created?.[0][0]).toMatchObject({
      ticker: 'MSFT',
      sharesOwned: 10,
      purchasePrice: 300,
      currentPrice: 350,
      purchaseDate: '2026-01-15',
      currencyCode: 'USD',
    });
  });

  it('clears itself after emitting, so the same form can add a second holding', async () => {
    const wrapper = mount(AddStockWidget);

    await fill(wrapper);
    await wrapper.find('form').trigger('submit');

    const inputs = wrapper.findAll('input');

    expect(inputs[0].element.value).toBe('');
    expect(inputs[1].element.value).toBe('0');
    expect(wrapper.find('select').element.value).toBe('EUR');
  });
});
