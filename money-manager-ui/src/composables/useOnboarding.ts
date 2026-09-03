import { computed, onMounted, ref } from 'vue';
import type { ComputedRef } from 'vue';
import type { RouteLocationRaw } from 'vue-router';
import { featureFlags } from '../services/features';
import type { FeatureName, Features } from '../services/features';
import { fetchOnboardingProgress } from '../services/onboarding';
import type { OnboardingProgress } from '../services/onboarding';

/**
 * "I have seen this and want it gone" is a fact about a person on a device, so it lives in
 * `localStorage` next to the locale. Progress itself stays derived and stored nowhere — the two
 * are different kinds of thing and only one of them belongs in the schema.
 */
export const ONBOARDING_DISMISSED_KEY = 'onboarding-dismissed';

/**
 * Per-step declines, same reasoning as dismissal: a person's choice on a device, not a fact about
 * the portfolio. Declining a required step counts as resolved for `isChecklistNeeded`, same as
 * done — the point is only to stop the panel nagging about something the landlord has said they
 * don't want, not to pretend the step happened.
 */
export const ONBOARDING_DECLINED_KEY = 'onboarding-declined';

export interface OnboardingStep {
  id: string;
  /** Where "Go" leads — a deep-link with a highlight where the form is unambiguous, the plain list page otherwise. */
  goTo: RouteLocationRaw;
  done: boolean;
  /**
   * Optional steps never hold the checklist open. A landlord who never records a valuation —
   * documented as optional, since purchase price is the stated fallback — must not be nagged
   * forever by a panel that cannot be satisfied.
   */
  optional: boolean;
  /** Declined by the person on this device — resolved, same as done, but distinct in the UI. */
  declined: boolean;
}

interface StepDefinition {
  id: string;
  to: string;
  optional: boolean;
  /** `null` for the core property steps, which every deployment has. */
  feature: FeatureName | null;
  done: (progress: OnboardingProgress) => boolean;
  guided: 'inline' | 'perProperty' | 'none';
}

/**
 * The order a landlord would actually do them in: a property, who is in it, what it has earned
 * and cost, then what it is worth.
 *
 * <p>The last three are gated on the flags rather than listed unconditionally. That is the whole
 * reason this reads `featureFlags`: an onboarding checklist that tells a Hungarian landlord to
 * connect a bank account and add stock holdings is describing an app they were not given, since
 * those sections are deliberately switched off and their endpoints answer 404.</p>
 */
const DEFINITIONS: StepDefinition[] = [
  {
    id: 'property',
    to: '/properties',
    optional: false,
    feature: null,
    done: (p) => p.hasProperty,
    guided: 'inline',
  },
  {
    id: 'tenancy',
    to: '/properties',
    optional: false,
    feature: null,
    done: (p) => p.hasLease,
    guided: 'perProperty',
  },
  {
    id: 'ledger',
    to: '/properties',
    optional: false,
    feature: null,
    done: (p) => p.hasTransaction,
    guided: 'perProperty',
  },
  {
    id: 'valuation',
    to: '/properties',
    optional: true,
    feature: null,
    done: (p) => p.hasValuation,
    guided: 'perProperty',
  },
  {
    id: 'bankAccount',
    to: '/accounts',
    optional: true,
    feature: 'banking',
    done: (p) => p.hasBankAccount,
    guided: 'none',
  },
  {
    id: 'loan',
    to: '/loans',
    optional: true,
    feature: 'loans',
    done: (p) => p.hasLoan,
    guided: 'inline',
  },
  {
    id: 'holding',
    to: '/stocks',
    optional: true,
    feature: 'stocks',
    done: (p) => p.hasStock,
    guided: 'none',
  },
];

/** Exposed for the test that checks every step has a label in every locale. */
export const STEP_IDS = DEFINITIONS.map((definition) => definition.id);

/** Exposed so pages can read which step, if any, a deep-link is asking them to highlight. */
export const ONBOARDING_QUERY_PARAM = 'onboarding';

function guidedTarget(definition: StepDefinition, solePropertyId: number | null): RouteLocationRaw {
  if (definition.guided === 'inline') {
    return { path: definition.to, query: { [ONBOARDING_QUERY_PARAM]: definition.id } };
  }

  if (definition.guided === 'perProperty' && solePropertyId !== null) {
    return {
      path: `/properties/${solePropertyId}`,
      query: { [ONBOARDING_QUERY_PARAM]: definition.id },
    };
  }

  // 2+ candidate properties (ambiguous), no candidate at all, or a step with no guided form yet
  // (bankAccount/holding — see the issue that added guided routing) — today's plain navigation.
  return { path: definition.to };
}

export function buildSteps(
  progress: OnboardingProgress,
  features: Readonly<Features>,
  declined: ReadonlySet<string> = new Set()
): OnboardingStep[] {
  return DEFINITIONS.filter(
    (definition) => definition.feature === null || features[definition.feature]
  ).map((definition) => ({
    id: definition.id,
    goTo: guidedTarget(definition, progress.solePropertyId),
    optional: definition.optional,
    done: definition.done(progress),
    declined: declined.has(definition.id),
  }));
}

/**
 * Whether the checklist has anything left to say. Only the required steps count, so the panel
 * disappears on its own once the portfolio is set up rather than waiting to be dismissed — and a
 * declined required step is resolved the same way a done one is, since nagging about a step the
 * landlord has explicitly said no to defeats the point of letting them decline it.
 */
export function isChecklistNeeded(steps: OnboardingStep[]): boolean {
  return steps.some((step) => !step.optional && !step.done && !step.declined);
}

function readDismissed(): boolean {
  try {
    return localStorage.getItem(ONBOARDING_DISMISSED_KEY) === 'true';
  } catch {
    // A browser with storage disabled should see the checklist, not a blank dashboard.
    return false;
  }
}

function storeDismissed(): void {
  try {
    localStorage.setItem(ONBOARDING_DISMISSED_KEY, 'true');
  } catch {
    // A session that cannot persist the choice should still honour it for this session.
  }
}

function readDeclined(): Set<string> {
  try {
    const raw = localStorage.getItem(ONBOARDING_DECLINED_KEY);
    const ids: unknown = raw ? JSON.parse(raw) : [];
    return new Set(Array.isArray(ids) ? ids.filter((id) => typeof id === 'string') : []);
  } catch {
    return new Set();
  }
}

function storeDeclined(declined: ReadonlySet<string>): void {
  try {
    localStorage.setItem(ONBOARDING_DECLINED_KEY, JSON.stringify([...declined]));
  } catch {
    // A session that cannot persist the choice should still honour it for this session.
  }
}

export function useOnboarding(): {
  steps: ComputedRef<OnboardingStep[]>;
  visible: ComputedRef<boolean>;
  dismiss: () => void;
  decline: (stepId: string) => void;
} {
  const progress = ref<OnboardingProgress | null>(null);
  const dismissed = ref(readDismissed());
  const declined = ref(readDeclined());

  const steps = computed(() =>
    progress.value === null ? [] : buildSteps(progress.value, featureFlags.value, declined.value)
  );

  // Nothing renders until the answer is in. Showing a checklist of outstanding steps to an
  // established landlord for the moment the request takes would be worse than showing nothing:
  // it would flash a panel telling them to add their first property.
  const visible = computed(
    () => !dismissed.value && progress.value !== null && isChecklistNeeded(steps.value)
  );

  onMounted(async () => {
    // Dismissed is already decided from localStorage, so no answer this fetch could bring back
    // would change `visible` — the same reasoning against moving rows to answer a settled
    // question, applied to a round-trip whose answer is already known.
    if (dismissed.value) return;

    try {
      progress.value = await fetchOnboardingProgress();
    } catch (error) {
      // The dashboard is not broken because onboarding could not load; the panel simply does not
      // appear. An established landlord — the overwhelmingly common case — sees no difference.
      console.error('Failed to load onboarding progress:', error);
    }
  });

  function dismiss(): void {
    dismissed.value = true;
    storeDismissed();
  }

  function decline(stepId: string): void {
    const next = new Set(declined.value);
    next.add(stepId);
    declined.value = next;
    storeDeclined(next);
  }

  return { steps, visible, dismiss, decline };
}
