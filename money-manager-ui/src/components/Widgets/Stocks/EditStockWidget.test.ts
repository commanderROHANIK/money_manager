/**
 * Mirrors AddStockWidget's fixture shape, but the contract is different: this widget seeds
 * itself from the holding it was handed and keeps the id on submit, since a PUT with no id would
 * have nothing to address.
 */
import { describe, it, expect } from 'vitest';
import { mount } from '@vue/test-utils';
import EditStockWidget from './EditStockWidget.vue';
import type { Stock } from '../../../models/models';

const stock: Stock = {
  id: 9,
  ticker: 'MSFT',
  sharesOwned: 10,
  purchasePrice: 300,
  currentPrice: 350,
  purchaseDate: '2026-01-15T00:00:00.000Z',
  currencyCode: 'USD',
};

describe('EditStockWidget', () => {
  it('seeds the form from the holding it was handed', () => {
    const wrapper = mount(EditStockWidget, { props: { stock } });

    const inputs = wrapper.findAll('input');
    expect(inputs[0].element.value).toBe('MSFT');
    expect(inputs[1].element.value).toBe('10');
    expect(inputs[2].element.value).toBe('300');
    expect(inputs[3].element.value).toBe('350');
    expect(inputs[4].element.value).toBe('2026-01-15');
    expect(wrapper.find('select').element.value).toBe('USD');
  });

  it('emits the edited fields together with the original id', async () => {
    const wrapper = mount(EditStockWidget, { props: { stock } });

    const inputs = wrapper.findAll('input');
    await inputs[1].setValue('12');
    await inputs[3].setValue('365');
    await wrapper.find('form').trigger('submit');

    const updated = wrapper.emitted('update');

    expect(updated).toHaveLength(1);
    expect(updated?.[0][0]).toMatchObject({
      id: 9,
      ticker: 'MSFT',
      sharesOwned: 12,
      purchasePrice: 300,
      currentPrice: 365,
      purchaseDate: '2026-01-15',
      currencyCode: 'USD',
    });
  });
});
