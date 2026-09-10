/**
 * The checklist's own interactive bits: declining a step, and the guided "Go" link carrying the
 * deep-link `useOnboarding` built for it. `useOnboarding.test.ts` covers the pure logic; this is
 * the template wiring on top of it — the part a pure-function test cannot see, like whether the
 * skip button actually disappears once a step is declined.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import OnboardingChecklist from './OnboardingChecklist.vue';
import { setLocale } from '../../../i18n';
import { DEFAULT_LOCALE } from '../../../i18n/locale';

const fetchProgress = vi.fn();

vi.mock('../../../services/onboarding', () => ({
  fetchOnboardingProgress: () => fetchProgress(),
}));

const nothing = {
  hasProperty: false,
  hasLease: false,
  hasTransaction: false,
  hasValuation: false,
  hasBankAccount: false,
  hasLoan: false,
  hasStock: false,
  solePropertyId: null,
};

const RouterLinkStub = { props: ['to'], template: '<a><slot /></a>' };

async function mountChecklist() {
  const wrapper = mount(OnboardingChecklist, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  await flushPromises();
  return wrapper;
}

beforeEach(() => {
  setLocale('en');
});

afterEach(() => {
  localStorage.clear();
  fetchProgress.mockReset();
  setLocale(DEFAULT_LOCALE);
});

describe('OnboardingChecklist', () => {
  it('renders a guided Go link for the property step, deep-linking with the step id', async () => {
    fetchProgress.mockResolvedValue(nothing);
    const wrapper = await mountChecklist();

    const propertyRow = wrapper
      .findAll('li')
      .find((li) => li.text().includes('Add your first property'));

    const goLink = propertyRow!.findComponent(RouterLinkStub);
    expect(goLink.props('to')).toEqual({ path: '/properties', query: { onboarding: 'property' } });
  });

  it('declining a step marks it skipped and removes its Go/Skip actions', async () => {
    fetchProgress.mockResolvedValue(nothing);
    const wrapper = await mountChecklist();

    const propertyRow = wrapper
      .findAll('li')
      .find((li) => li.text().includes('Add your first property'));
    expect(propertyRow).toBeDefined();

    await propertyRow!.find('button').trigger('click');

    expect(propertyRow!.text()).toContain('Skipped');
    expect(propertyRow!.find('a').exists()).toBe(false);
    expect(propertyRow!.findAll('button')).toHaveLength(0);

    // The other required steps are untouched, so the panel is still up.
    expect(wrapper.find('h2').text()).toBe('Getting started');
  });

  it('ticks a done step and offers it no Go or Skip action', async () => {
    fetchProgress.mockResolvedValue({ ...nothing, hasProperty: true });
    const wrapper = await mountChecklist();

    const propertyRow = wrapper
      .findAll('li')
      .find((li) => li.text().includes('Add your first property'));

    expect(propertyRow!.text()).toContain('Done');
    expect(propertyRow!.find('a').exists()).toBe(false);
    expect(propertyRow!.findAll('button')).toHaveLength(0);
  });

  it('deep-links a per-property step to the sole property when there is exactly one', async () => {
    fetchProgress.mockResolvedValue({ ...nothing, solePropertyId: 5 });
    const wrapper = await mountChecklist();

    const tenancyRow = wrapper.findAll('li').find((li) => li.text().includes('Record who is renting it'));

    const goLink = tenancyRow!.findComponent(RouterLinkStub);
    expect(goLink.props('to')).toEqual({ path: '/properties/5', query: { onboarding: 'tenancy' } });
  });

  it('remembers a decline across a remount', async () => {
    fetchProgress.mockResolvedValue(nothing);
    const first = await mountChecklist();

    const propertyRow = first.findAll('li').find((li) => li.text().includes('Add your first property'));
    await propertyRow!.find('button').trigger('click');

    const second = await mountChecklist();
    const propertyRowAgain = second
      .findAll('li')
      .find((li) => li.text().includes('Add your first property'));

    expect(propertyRowAgain!.text()).toContain('Skipped');
  });
});
