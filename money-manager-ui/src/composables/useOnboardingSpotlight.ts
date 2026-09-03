import { computed } from 'vue';
import type { ComputedRef } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ONBOARDING_QUERY_PARAM } from './useOnboarding';

/**
 * Reads the `?onboarding=<stepId>` a guided checklist "Go" link leaves behind, and turns it into
 * a highlight the destination page can put a ring around and explain — the deep-link half of
 * "guided Go", the part `useOnboarding`'s `goTo` cannot do by itself since it only builds the
 * link, not what the landing page does with it.
 *
 * <p>Only one step is ever active per page, since only one `onboarding` query value can be
 * present at a time — `stepIds` just says which of this page's sections may claim it.</p>
 */
export function useOnboardingSpotlight(stepIds: readonly string[]): {
  active: ComputedRef<string | null>;
  isActive: (stepId: string) => boolean;
  clear: () => void;
} {
  const route = useRoute();
  const router = useRouter();

  const active = computed(() => {
    const raw = route.query[ONBOARDING_QUERY_PARAM];
    const id = Array.isArray(raw) ? raw[0] : raw;
    return id && stepIds.includes(id) ? id : null;
  });

  function isActive(stepId: string): boolean {
    return active.value === stepId;
  }

  // Called once the step the deep-link was guiding toward is actually done, so the highlight
  // disappears because the thing it was pointing at happened — not on a timer, and not by the
  // visitor having to notice and dismiss it themselves.
  function clear(): void {
    if (active.value === null) return;

    const query = { ...route.query };
    delete query[ONBOARDING_QUERY_PARAM];
    router.replace({ query });
  }

  return { active, isActive, clear };
}
