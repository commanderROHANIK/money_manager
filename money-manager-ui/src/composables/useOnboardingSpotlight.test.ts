/**
 * The landing half of guided "Go": given a route carrying `?onboarding=<stepId>`, does this page
 * think it owns that step, and does declaring the step done clear the query rather than leaving
 * a highlight pointed at nothing.
 */
import { describe, it, expect, vi, afterEach } from 'vitest';
import { defineComponent, h } from 'vue';
import { mount } from '@vue/test-utils';
import { useOnboardingSpotlight } from './useOnboardingSpotlight';

let query: Record<string, string> = {};
const replace = vi.fn();

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>();
  return {
    ...actual,
    useRoute: () => ({ query }),
    useRouter: () => ({ replace }),
  };
});

function mountSpotlight(stepIds: string[]) {
  let api: ReturnType<typeof useOnboardingSpotlight> | null = null;

  mount(
    defineComponent({
      setup() {
        api = useOnboardingSpotlight(stepIds);
        return () => h('div');
      },
    })
  );

  return api as unknown as ReturnType<typeof useOnboardingSpotlight>;
}

afterEach(() => {
  query = {};
  replace.mockClear();
});

describe('useOnboardingSpotlight', () => {
  it('is inactive when the URL carries no onboarding step', () => {
    const { active, isActive } = mountSpotlight(['property']);

    expect(active.value).toBeNull();
    expect(isActive('property')).toBe(false);
  });

  it('claims the step named on the URL when this page owns it', () => {
    query = { onboarding: 'tenancy' };

    const { active, isActive } = mountSpotlight(['tenancy', 'ledger']);

    expect(active.value).toBe('tenancy');
    expect(isActive('tenancy')).toBe(true);
    expect(isActive('ledger')).toBe(false);
  });

  it('ignores a step named on the URL that belongs to a different page', () => {
    // A stale or hand-edited link pointing at a step this page never renders — treated the same
    // as no query at all, not as an error.
    query = { onboarding: 'bankAccount' };

    const { active, isActive } = mountSpotlight(['property']);

    expect(active.value).toBeNull();
    expect(isActive('property')).toBe(false);
  });

  it('does nothing on clear() when nothing is active', () => {
    const { clear } = mountSpotlight(['property']);

    clear();

    expect(replace).not.toHaveBeenCalled();
  });

  it('drops only the onboarding key on clear(), keeping the rest of the query', () => {
    query = { onboarding: 'property', foo: 'bar' };

    const { clear } = mountSpotlight(['property']);

    clear();

    expect(replace).toHaveBeenCalledWith({ query: { foo: 'bar' } });
  });
});
