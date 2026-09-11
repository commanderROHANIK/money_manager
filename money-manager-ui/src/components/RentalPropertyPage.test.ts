/**
 * The onboarding guided-Go landing behaviour on the properties page: no spotlight without a
 * matching `?onboarding=` query, a highlighted add-property form with one, and the highlight
 * clearing once the property is actually created — see `useOnboardingSpotlight`.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import RentalPropertyPage from './RentalPropertyPage.vue';
import AddPropertyWidget from './Widgets/Properties/AddPropertyWidget.vue';
import * as f from '../__tests__/fixtures';
import { setLocale } from '../i18n';
import { DEFAULT_LOCALE } from '../i18n/locale';

// Chart.js needs a real canvas; jsdom has none. The chart widgets on this page are not what this
// test is about, so stand them down to placeholders, same as widgets.smoke.test.ts.
vi.mock('vue-chartjs', async () => {
  const { defineComponent, h } = await import('vue');
  const stub = (name: string) => defineComponent({ name, render: () => h('canvas') });
  return { Bar: stub('Bar'), Line: stub('Line'), Pie: stub('Pie'), Doughnut: stub('Doughnut') };
});

let query: Record<string, string> = {};
const replace = vi.fn();

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>();
  return { ...actual, useRoute: () => ({ query }), useRouter: () => ({ replace }) };
});

vi.mock('../services/api', () => ({
  extractApiError: () => ({ fields: {}, message: 'failed' }),
  fetchRentalProperties: () => Promise.resolve(f.properties),
  deleteRentalProperty: () => Promise.resolve(),
}));

const createProperty = vi.fn();

vi.mock('../services/propertyApi', () => ({
  createProperty: (request: unknown) => createProperty(request),
  fetchArrears: () => Promise.resolve(f.arrears),
  fetchPortfolioAnalytics: () => Promise.resolve(f.portfolio),
}));

function mountPage() {
  return mount(RentalPropertyPage, {
    global: { stubs: { RouterLink: { template: '<a><slot /></a>' } } },
  });
}

beforeEach(() => {
  query = {};
  replace.mockClear();
  createProperty.mockReset();
  setLocale('en');
  // jsdom does not implement scrollIntoView; the spotlight calls it once it locates its target.
  Element.prototype.scrollIntoView = vi.fn();
});

afterEach(() => {
  setLocale(DEFAULT_LOCALE);
});

describe('RentalPropertyPage', () => {
  it('renders no spotlight when no onboarding step is being guided here', async () => {
    const wrapper = mountPage();
    await flushPromises();

    expect(wrapper.text()).not.toContain('Fill in the form below to add your first property.');
  });

  it('highlights the add-property form when guided here for the property step', async () => {
    query = { onboarding: 'property' };
    const wrapper = mountPage();
    await flushPromises();

    expect(wrapper.text()).toContain('Fill in the form below to add your first property.');
    expect(Element.prototype.scrollIntoView).toHaveBeenCalled();
  });

  it('clears the spotlight once the property is actually created', async () => {
    query = { onboarding: 'property' };
    createProperty.mockResolvedValue(undefined);
    const wrapper = mountPage();
    await flushPromises();

    await wrapper.findComponent(AddPropertyWidget).vm.$emit('create', {
      propertyName: 'Test',
      address: 'x',
      city: null,
      propertyType: 0,
      purchasePrice: null,
      purchaseDate: null,
      status: 0,
      currencyCode: 'EUR',
      sizeSqm: null,
      bedrooms: null,
    });
    await flushPromises();

    expect(createProperty).toHaveBeenCalled();
    expect(replace).toHaveBeenCalledWith({ query: {} });
  });
});
