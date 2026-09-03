/**
 * Thin, but the path is worth pinning: the widget builds its steps from these field names, and a
 * rename on either side would render a checklist where nothing is ever ticked — a "you have done
 * none of this" panel shown to somebody with a full portfolio, which no type check would catch
 * across the HTTP boundary.
 */
import { describe, it, expect, vi, afterEach } from 'vitest';
import type { AxiosAdapter, AxiosRequestConfig } from 'axios';
import type { OnboardingProgress } from './onboarding';

const established: OnboardingProgress = {
  hasProperty: true,
  hasLease: true,
  hasTransaction: true,
  hasValuation: false,
  hasBankAccount: false,
  hasLoan: true,
  hasStock: false,
  solePropertyId: 7,
};

async function load(respond: () => Promise<OnboardingProgress>) {
  vi.resetModules();

  const { api } = await import('./api');
  const seen: AxiosRequestConfig[] = [];

  const adapter: AxiosAdapter = async (config) => {
    seen.push(config);
    return { data: await respond(), status: 200, statusText: 'OK', headers: {}, config };
  };

  api.defaults.adapter = adapter;

  const onboarding = await import('./onboarding');

  return { ...onboarding, seen };
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('fetchOnboardingProgress', () => {
  it('asks the onboarding endpoint', async () => {
    const { fetchOnboardingProgress, seen } = await load(async () => established);

    await fetchOnboardingProgress();

    expect(seen).toHaveLength(1);
    expect(seen[0].url).toBe('/Onboarding');
  });

  it('returns the flags as the API reported them', async () => {
    const { fetchOnboardingProgress } = await load(async () => established);

    // Field-for-field rather than a spot check: a partial assertion would pass while a renamed
    // field arrived as undefined, which reads as "not done" in the checklist.
    expect(await fetchOnboardingProgress()).toEqual(established);
  });
});
