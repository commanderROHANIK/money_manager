/**
 * The scroll-to-and-explain half of guided onboarding. The claim under test is narrow but exact:
 * an organic visit (no `?onboarding=<stepId>` match) renders the slot untouched, and a guided one
 * adds the explanation and scrolls to it — nothing in between.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import { setLocale } from '../../i18n';
import { DEFAULT_LOCALE } from '../../i18n/locale';
import OnboardingGuideHighlight from './OnboardingGuideHighlight.vue';

let query: Record<string, string> = {};

// Guide copy is asserted in English; the default locale is Hungarian, so this test would
// otherwise be checking the wrong language while appearing to test the right thing.
beforeEach(() => setLocale('en'));
afterEach(() => setLocale(DEFAULT_LOCALE));

// Not just useRoute, unlike Menu.test.ts: useOnboardingGuide also calls useRouter() (for
// clear()), which none of these three cases exercise today — but leaving it as the real
// implementation would mean a future test that does exercise clear() hits an unmocked router
// with no provider, rather than a deliberate stub.
vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>();
  return { ...actual, useRoute: () => ({ query }), useRouter: () => ({ replace: vi.fn() }) };
});

function mountWith(stepId: string) {
  return mount(OnboardingGuideHighlight, {
    props: { stepId },
    slots: { default: '<p>the form</p>' },
  });
}

describe('OnboardingGuideHighlight', () => {
  it('renders the slot untouched on an organic visit', () => {
    query = {};

    const wrapper = mountWith('property');

    expect(wrapper.text()).toContain('the form');
    expect(wrapper.text()).not.toContain('Fill in the form below');
  });

  it('renders the slot untouched when the param names a different step', () => {
    query = { onboarding: 'loan' };

    const wrapper = mountWith('property');

    expect(wrapper.text()).not.toContain('Fill in the form below');
  });

  it('adds the explanation and scrolls to it when the param matches', async () => {
    query = { onboarding: 'property' };
    // jsdom has no layout engine, so scrollIntoView does not exist unless a test provides one.
    const scrollIntoView = vi.fn();
    Element.prototype.scrollIntoView = scrollIntoView;

    const wrapper = mountWith('property');
    await wrapper.vm.$nextTick();
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('Fill in the form below to add your property.');
    expect(scrollIntoView).toHaveBeenCalled();
  });
});
