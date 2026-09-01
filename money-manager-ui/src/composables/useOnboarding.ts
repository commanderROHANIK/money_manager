import { computed, onMounted, ref } from 'vue';
import type { ComputedRef } from 'vue';
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

export interface OnboardingStep {
  id: string;
  /** Where the step is actually done, so the checklist is a set of links rather than a lecture. */
  to: string;
  done: boolean;
  /**
   * Optional steps never hold the checklist open. A landlord who never records a valuation —
   * documented as optional, since purchase price is the stated fallback — must not be nagged
   * forever by a panel that cannot be satisfied.
   */
  optional: boolean;
}

interface StepDefinition extends Omit<OnboardingStep, 'done'> {
  /** `null` for the core property steps, which every deployment has. */
  feature: FeatureName | null;
  done: (progress: OnboardingProgress) => boolean;
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
  { id: 'property', to: '/properties', optional: false, feature: null, done: (p) => p.hasProperty },
  { id: 'tenancy', to: '/properties', optional: false, feature: null, done: (p) => p.hasLease },
  { id: 'ledger', to: '/properties', optional: false, feature: null, done: (p) => p.hasTransaction },
  { id: 'valuation', to: '/properties', optional: true, feature: null, done: (p) => p.hasValuation },
  { id: 'bankAccount', to: '/accounts', optional: true, feature: 'banking', done: (p) => p.hasBankAccount },
  { id: 'loan', to: '/loans', optional: true, feature: 'loans', done: (p) => p.hasLoan },
  { id: 'holding', to: '/stocks', optional: true, feature: 'stocks', done: (p) => p.hasStock },
];

/** Exposed for the test that checks every step has a label in every locale. */
export const STEP_IDS = DEFINITIONS.map((definition) => definition.id);

export function buildSteps(
  progress: OnboardingProgress,
  features: Readonly<Features>
): OnboardingStep[] {
  return DEFINITIONS.filter(
    (definition) => definition.feature === null || features[definition.feature]
  ).map((definition) => ({
    id: definition.id,
    to: definition.to,
    optional: definition.optional,
    done: definition.done(progress),
  }));
}

/**
 * Whether the checklist has anything left to say. Only the required steps count, so the panel
 * disappears on its own once the portfolio is set up rather than waiting to be dismissed.
 */
export function isChecklistNeeded(steps: OnboardingStep[]): boolean {
  return steps.some((step) => !step.optional && !step.done);
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

export function useOnboarding(): {
  steps: ComputedRef<OnboardingStep[]>;
  visible: ComputedRef<boolean>;
  dismiss: () => void;
} {
  const progress = ref<OnboardingProgress | null>(null);
  const dismissed = ref(readDismissed());

  const steps = computed(() =>
    progress.value === null ? [] : buildSteps(progress.value, featureFlags.value)
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

  return { steps, visible, dismiss };
}
