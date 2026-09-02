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

/**
 * "I don't want to do this one" is the same kind of fact as dismissing the whole panel — a
 * per-device preference, not progress — so it lives next to it rather than being derived.
 */
export const ONBOARDING_DECLINED_KEY = 'onboarding-declined-steps';

export interface OnboardingStep {
  id: string;
  /**
   * Where "Go" actually sends someone. For a step with a guided target this carries
   * `?onboarding=<id>`, which the destination page reads to scroll to and explain the relevant
   * input — see `resolveTo`. Plain navigation (no param) when there is nothing to guide toward
   * yet, or nothing unambiguous to guide toward.
   */
  to: string;
  done: boolean;
  declined: boolean;
  /**
   * Optional steps never hold the checklist open. A landlord who never records a valuation —
   * documented as optional, since purchase price is the stated fallback — must not be nagged
   * forever by a panel that cannot be satisfied.
   */
  optional: boolean;
}

interface StepDefinition extends Omit<OnboardingStep, 'done' | 'declined' | 'to'> {
  /** The page a plain (non-guided) visit lands on — unchanged from before guided routing existed. */
  basePath: string;
  /** `null` for the core property steps, which every deployment has. */
  feature: FeatureName | null;
  done: (progress: OnboardingProgress) => boolean;
}

/**
 * The three steps whose form lives per-property (`PropertyDetailPage.vue`) even though what they
 * check is portfolio-wide ("does any property have a lease/transaction/valuation"). Guiding to
 * "the" property only makes sense when there is exactly one candidate — see `resolveTo`.
 */
const PROPERTY_SCOPED_STEPS = new Set(['tenancy', 'ledger', 'valuation']);

/**
 * Where "Go" actually sends someone for a given step.
 *
 * <p><c>property</c> and <c>loan</c> already have their add-form inline on the page `basePath`
 * points at, so guiding there is unambiguous: land on the page, highlight the form. The three
 * property-scoped steps guide straight to the one property missing them when there is exactly
 * one candidate (`progress.soleRentalPropertyId`); with zero or several, which property to send
 * someone to is a guess this app does not make, so it falls back to the plain list page — the
 * same place "Go" has always pointed. <c>bankAccount</c>/<c>holding</c> have no working add-form
 * to guide toward yet (tracked separately), so they stay plain until that exists.</p>
 */
function resolveTo(id: string, basePath: string, progress: OnboardingProgress): string {
  if (id === 'property' || id === 'loan') {
    return `${basePath}?onboarding=${id}`;
  }

  if (PROPERTY_SCOPED_STEPS.has(id)) {
    return progress.soleRentalPropertyId === null
      ? basePath
      : `/properties/${progress.soleRentalPropertyId}?onboarding=${id}`;
  }

  return basePath;
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
  { id: 'property', basePath: '/properties', optional: false, feature: null, done: (p) => p.hasProperty },
  { id: 'tenancy', basePath: '/properties', optional: false, feature: null, done: (p) => p.hasLease },
  { id: 'ledger', basePath: '/properties', optional: false, feature: null, done: (p) => p.hasTransaction },
  { id: 'valuation', basePath: '/properties', optional: true, feature: null, done: (p) => p.hasValuation },
  { id: 'bankAccount', basePath: '/accounts', optional: true, feature: 'banking', done: (p) => p.hasBankAccount },
  { id: 'loan', basePath: '/loans', optional: true, feature: 'loans', done: (p) => p.hasLoan },
  { id: 'holding', basePath: '/stocks', optional: true, feature: 'stocks', done: (p) => p.hasStock },
];

/** Exposed for the test that checks every step has a label in every locale. */
export const STEP_IDS = DEFINITIONS.map((definition) => definition.id);

export function buildSteps(
  progress: OnboardingProgress,
  features: Readonly<Features>,
  declinedIds: ReadonlySet<string> = new Set()
): OnboardingStep[] {
  return DEFINITIONS.filter(
    (definition) => definition.feature === null || features[definition.feature]
  ).map((definition) => ({
    id: definition.id,
    to: resolveTo(definition.id, definition.basePath, progress),
    optional: definition.optional,
    done: definition.done(progress),
    declined: declinedIds.has(definition.id),
  }));
}

/**
 * Whether the checklist has anything left to say. Only the required steps count, and a declined
 * step counts as resolved the same as a done one — "I don't want to do this" closes the question
 * as surely as doing it does, so the panel disappears on its own rather than staying stuck open
 * on a required step nobody is going to complete.
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
    const ids: unknown = raw === null ? [] : JSON.parse(raw);

    return new Set(Array.isArray(ids) ? ids.filter((id): id is string => typeof id === 'string') : []);
  } catch {
    // Malformed or inaccessible storage should show every step, not hide them all as declined.
    return new Set();
  }
}

function storeDeclined(ids: ReadonlySet<string>): void {
  try {
    localStorage.setItem(ONBOARDING_DECLINED_KEY, JSON.stringify([...ids]));
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
    // Reassigned rather than mutated in place, matching `dismissed` above — plain and explicit
    // rather than leaning on Vue's Set-proxy reactivity.
    declined.value = new Set(declined.value).add(stepId);
    storeDeclined(declined.value);
  }

  return { steps, visible, dismiss, decline };
}
