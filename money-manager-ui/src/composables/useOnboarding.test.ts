/**
 * The onboarding checklist's whole design claim is that progress is *derived*, so these tests are
 * mostly about the two pure functions. A stored flag would pass a "new account sees the checklist"
 * test just as happily; what it would fail is the one where the only property is deleted.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { defineComponent, h } from 'vue';
import { mount, flushPromises } from '@vue/test-utils';
import de from '../locales/de.json';
import en from '../locales/en.json';
import fr from '../locales/fr.json';
import hu from '../locales/hu.json';
import {
  buildSteps,
  isChecklistNeeded,
  ONBOARDING_DECLINED_KEY,
  ONBOARDING_DISMISSED_KEY,
  STEP_IDS,
  useOnboarding,
} from './useOnboarding';
import type { OnboardingProgress } from '../services/onboarding';
import type { Features } from '../services/features';

const fetchProgress = vi.fn();

vi.mock('../services/onboarding', () => ({
  fetchOnboardingProgress: () => fetchProgress(),
}));

let userId: string | null = null;

vi.mock('../services/authService', () => ({
  currentUserId: () => userId,
}));

const nothing: OnboardingProgress = {
  hasProperty: false,
  soleRentalPropertyId: null,
  hasLease: false,
  hasTransaction: false,
  hasValuation: false,
  hasBankAccount: false,
  hasLoan: false,
  hasStock: false,
};

/** The MVP posture: banking and stocks deliberately switched off. */
const mvp: Features = {
  banking: false,
  stocks: false,
  loans: true,
  events: true,
  automaticExchangeRates: true,
};

const everything: Features = {
  banking: true,
  stocks: true,
  loans: true,
  events: true,
  automaticExchangeRates: true,
};

const idsOf = (features: Features, progress = nothing) =>
  buildSteps(progress, features).map((step) => step.id);

describe('buildSteps', () => {
  it('offers every core step to an account that has done nothing', () => {
    const steps = buildSteps(nothing, mvp);

    expect(steps.filter((step) => step.done)).toEqual([]);
    expect(idsOf(mvp)).toContain('property');
    expect(idsOf(mvp)).toContain('tenancy');
    expect(idsOf(mvp)).toContain('ledger');
  });

  it('never mentions a section the deployment has switched off', () => {
    // The defect in the issue, stated directly: an onboarding checklist telling a Hungarian
    // landlord to connect a bank account and add stock holdings is describing a different app.
    expect(idsOf(mvp)).not.toContain('bankAccount');
    expect(idsOf(mvp)).not.toContain('holding');

    // And the section that *is* on still appears, so this is filtering rather than omitting.
    expect(idsOf(mvp)).toContain('loan');
  });

  it('offers the gated steps when those sections are on', () => {
    expect(idsOf(everything)).toContain('bankAccount');
    expect(idsOf(everything)).toContain('holding');
  });

  it('ticks a step from the data rather than from a flag', () => {
    const withProperty = buildSteps({ ...nothing, hasProperty: true }, mvp);

    expect(withProperty.find((step) => step.id === 'property')?.done).toBe(true);
    expect(withProperty.find((step) => step.id === 'tenancy')?.done).toBe(false);
  });

  it('un-ticks a step when the thing behind it is gone', () => {
    // The test a stored "onboarded" column fails. Deleting the only property is not a rollback of
    // a decision — it is the disappearance of the fact the decision was read from.
    const before = buildSteps({ ...nothing, hasProperty: true }, mvp);
    const after = buildSteps(nothing, mvp);

    expect(before.find((step) => step.id === 'property')?.done).toBe(true);
    expect(after.find((step) => step.id === 'property')?.done).toBe(false);
  });

  it('marks a step declined only when its id is in the declined set', () => {
    const steps = buildSteps(nothing, mvp, new Set(['valuation']));

    expect(steps.find((step) => step.id === 'valuation')?.declined).toBe(true);
    expect(steps.find((step) => step.id === 'property')?.declined).toBe(false);
  });

  it('guides Go to the sole property for tenancy/ledger/valuation, plainly otherwise', () => {
    const ambiguous = buildSteps({ ...nothing, soleRentalPropertyId: null }, mvp);
    const unambiguous = buildSteps({ ...nothing, soleRentalPropertyId: 7 }, mvp);

    // No candidate, or more than one: no guess, same plain list page as always.
    expect(ambiguous.find((step) => step.id === 'tenancy')?.to).toBe('/properties');

    // Exactly one candidate: straight to it, carrying the step id for the target page to explain.
    expect(unambiguous.find((step) => step.id === 'tenancy')?.to).toBe('/properties/7?onboarding=tenancy');
    expect(unambiguous.find((step) => step.id === 'ledger')?.to).toBe('/properties/7?onboarding=ledger');
    expect(unambiguous.find((step) => step.id === 'valuation')?.to).toBe(
      '/properties/7?onboarding=valuation'
    );
  });

  it('guides Go for property and loan without needing a property id', () => {
    const steps = buildSteps(nothing, mvp);

    expect(steps.find((step) => step.id === 'property')?.to).toBe('/properties?onboarding=property');
    expect(steps.find((step) => step.id === 'loan')?.to).toBe('/loans?onboarding=loan');
  });

  it('never guides bankAccount or holding — no working form to guide toward yet', () => {
    const steps = buildSteps({ ...nothing, soleRentalPropertyId: 7 }, everything);

    expect(steps.find((step) => step.id === 'bankAccount')?.to).toBe('/accounts');
    expect(steps.find((step) => step.id === 'holding')?.to).toBe('/stocks');
  });
});

describe('isChecklistNeeded', () => {
  it('is true while a required step is outstanding', () => {
    expect(isChecklistNeeded(buildSteps(nothing, mvp))).toBe(true);
  });

  it('is false once the required steps are done, even with optional ones left', () => {
    const setUp = buildSteps(
      { ...nothing, hasProperty: true, hasLease: true, hasTransaction: true },
      mvp
    );

    // Valuation and the mortgage are still outstanding and deliberately do not hold the panel
    // open: a landlord who never records a valuation must not be nagged by a checklist that
    // cannot be satisfied. This is what makes it safe to mount unconditionally on the dashboard.
    expect(setUp.some((step) => !step.done)).toBe(true);
    expect(isChecklistNeeded(setUp)).toBe(false);
  });

  it('is false for an established portfolio', () => {
    const established: OnboardingProgress = {
      hasProperty: true,
      soleRentalPropertyId: null,
      hasLease: true,
      hasTransaction: true,
      hasValuation: true,
      hasBankAccount: true,
      hasLoan: true,
      hasStock: true,
    };

    expect(isChecklistNeeded(buildSteps(established, everything))).toBe(false);
  });

  it('treats a declined required step as resolved, the same as done', () => {
    // property is still outstanding but declined; tenancy and ledger are actually done. Nothing
    // required is left un-answered, so the panel has nothing to say.
    const declinedProperty = buildSteps(
      { ...nothing, hasLease: true, hasTransaction: true },
      mvp,
      new Set(['property'])
    );

    expect(declinedProperty.find((step) => step.id === 'property')?.done).toBe(false);
    expect(isChecklistNeeded(declinedProperty)).toBe(false);
  });

  it('stays true when a required step is merely outstanding, not declined', () => {
    const untouched = buildSteps(
      { ...nothing, hasLease: true, hasTransaction: true },
      mvp
    );

    expect(isChecklistNeeded(untouched)).toBe(true);
  });
});

describe('useOnboarding', () => {
  /**
   * Mounts something that does nothing but run the composable, and hands back what it returned.
   * One component definition for the whole file: the composable needs a mounted instance for its
   * `onMounted` fetch, and a second `defineComponent` here trips `vue/one-component-per-file`.
   */
  function mountComposable(): ReturnType<typeof useOnboarding> {
    let api: ReturnType<typeof useOnboarding> | null = null;

    mount(
      defineComponent({
        setup() {
          api = useOnboarding();
          return () => h('div');
        },
      })
    );

    return api as unknown as ReturnType<typeof useOnboarding>;
  }

  /** The same, once the fetch has settled. */
  async function run(): Promise<ReturnType<typeof useOnboarding>> {
    const api = mountComposable();
    await flushPromises();
    return api;
  }

  beforeEach(() => {
    localStorage.clear();
    fetchProgress.mockReset();
    fetchProgress.mockResolvedValue(nothing);
    userId = null;
  });

  it('shows nothing until the server has answered', async () => {
    // A request that never settles, so this observes the state during the load rather than after.
    fetchProgress.mockImplementation(() => new Promise(() => {}));

    const { visible } = mountComposable();

    // Flashing "add your first property" at an established landlord for the length of one request
    // is worse than showing nothing at all, so the panel waits rather than assuming.
    expect(visible.value).toBe(false);
  });

  it('appears for an account that has done nothing', async () => {
    const { visible } = await run();

    expect(visible.value).toBe(true);
  });

  it('stays hidden when the request fails', async () => {
    // The dashboard is not broken because onboarding could not load. Console noise is expected
    // here; the assertion is that nothing renders.
    vi.spyOn(console, 'error').mockImplementation(() => {});
    fetchProgress.mockRejectedValue(new Error('offline'));

    const { visible } = await run();

    expect(visible.value).toBe(false);
  });

  it('hides on dismissal and remembers it', async () => {
    const { visible, dismiss } = await run();

    expect(visible.value).toBe(true);

    dismiss();

    expect(visible.value).toBe(false);
    expect(localStorage.getItem(ONBOARDING_DISMISSED_KEY)).toBe('true');

    // Survives a reload: dismissal is a fact about a person on a device, which is why it is in
    // storage rather than in the schema.
    const next = await run();

    expect(next.visible.value).toBe(false);
  });

  it('declines a single step and remembers it, independently of dismiss', async () => {
    const { steps, visible, decline } = await run();

    decline('valuation');

    expect(steps.value.find((step) => step.id === 'valuation')?.declined).toBe(true);
    expect(JSON.parse(localStorage.getItem(ONBOARDING_DECLINED_KEY) ?? '[]')).toEqual(['valuation']);

    // valuation is optional and never held the panel open on its own, and every required step is
    // still genuinely outstanding — declining one optional step does not dismiss the whole panel.
    expect(visible.value).toBe(true);
    expect(localStorage.getItem(ONBOARDING_DISMISSED_KEY)).toBeNull();

    // Survives a reload, same as dismiss.
    const next = await run();

    expect(next.steps.value.find((step) => step.id === 'valuation')?.declined).toBe(true);
  });

  it('namespaces dismissed/declined by the logged-in user, so a shared device cannot leak one account into another', async () => {
    userId = 'alice';
    const alice = await run();

    alice.dismiss();
    alice.decline('valuation');

    expect(localStorage.getItem(`${ONBOARDING_DISMISSED_KEY}:alice`)).toBe('true');
    expect(JSON.parse(localStorage.getItem(`${ONBOARDING_DECLINED_KEY}:alice`) ?? '[]')).toEqual([
      'valuation',
    ]);

    // A different account on the same browser: neither the dismissal nor the decline carries
    // over — this is the regression a bare, unnamespaced key would have produced, since a
    // declined *required* step is treated as resolved and could otherwise hide Bob's checklist
    // entirely despite him having done nothing.
    userId = 'bob';
    const bob = await run();

    expect(bob.visible.value).toBe(true);
    expect(bob.steps.value.find((step) => step.id === 'valuation')?.declined).toBe(false);

    // Switching back to Alice still finds her own choices, unaffected by Bob's session in between.
    // The panel stays dismissed — and dismissed short-circuits the fetch itself (see
    // useOnboarding's onMounted), so this is read back from storage directly rather than
    // through `steps`, which stays empty when nothing was ever fetched.
    userId = 'alice';
    const aliceAgain = await run();

    expect(aliceAgain.visible.value).toBe(false);
    expect(JSON.parse(localStorage.getItem(`${ONBOARDING_DECLINED_KEY}:alice`) ?? '[]')).toEqual([
      'valuation',
    ]);
  });

  it('migrates a pre-namespacing bare-key value to the namespaced slot, once, for whoever reads it first', async () => {
    // Written before per-user namespacing existed, so it sits under the bare key exactly as a
    // real upgraded install would have it.
    localStorage.setItem(ONBOARDING_DISMISSED_KEY, 'true');
    localStorage.setItem(ONBOARDING_DECLINED_KEY, JSON.stringify(['valuation']));

    userId = 'alice';
    const alice = await run();

    // Alice inherits the pre-existing dismissal rather than seeing the panel reappear — dismissed
    // short-circuits the fetch itself (see useOnboarding's onMounted), so `steps` stays empty and
    // the declined value is checked from storage directly, same as the namespacing test above.
    expect(alice.visible.value).toBe(false);
    // ...and reading it moved it under her own key rather than leaving it readable by anyone else.
    expect(localStorage.getItem(`${ONBOARDING_DISMISSED_KEY}:alice`)).toBe('true');
    expect(JSON.parse(localStorage.getItem(`${ONBOARDING_DECLINED_KEY}:alice`) ?? '[]')).toEqual([
      'valuation',
    ]);
    expect(localStorage.getItem(ONBOARDING_DISMISSED_KEY)).toBeNull();
    expect(localStorage.getItem(ONBOARDING_DECLINED_KEY)).toBeNull();

    // The bare key is gone, so a second account on the same device does not also inherit it —
    // the leak this migration exists to bound to at most one login, not leave open indefinitely.
    userId = 'bob';
    const bob = await run();

    expect(bob.visible.value).toBe(true);
  });

  it('merges a decline against the latest storage rather than a stale in-memory snapshot, so a second tab cannot clobber the first', async () => {
    const tabA = await run();
    const tabB = await run();

    tabA.decline('valuation');
    // tabB mounted before tabA's decline landed in storage, so its own `declined` ref is still
    // empty here — the scenario the fix targets is exactly this: a decline issued from that
    // stale state must not overwrite what tabA already wrote.
    tabB.decline('bankAccount');

    expect(JSON.parse(localStorage.getItem(ONBOARDING_DECLINED_KEY) ?? '[]').sort()).toEqual([
      'bankAccount',
      'valuation',
    ]);
  });
});

describe('step labels', () => {
  const FILES = { hu, en, de, fr } as Record<string, Record<string, unknown>>;

  // The widget builds its keys as `onboarding.steps.<id>.title`, so a step added without copy
  // renders the raw key — and missingWarn is off, so nothing would say so. messages.test.ts
  // guarantees the four files agree with each other; this guarantees they cover the steps.
  it.each(Object.keys(FILES))('%s has a title and hint for every step', (locale) => {
    const steps = (FILES[locale].onboarding as { steps: Record<string, unknown> }).steps;

    for (const id of STEP_IDS) {
      const step = steps[id] as { title?: string; hint?: string } | undefined;

      expect(step, `${locale} is missing onboarding.steps.${id}`).toBeDefined();
      expect(step?.title, `${locale}: ${id}.title`).toBeTruthy();
      expect(step?.hint, `${locale}: ${id}.hint`).toBeTruthy();
    }
  });
});
