import { computed } from 'vue';
import type { ComputedRef } from 'vue';
import { useRoute } from 'vue-router';

/**
 * True when this page was reached via the onboarding checklist's guided "Go" for `stepId` —
 * carried as a `?onboarding=<stepId>` query param (see `resolveTo` in `useOnboarding.ts`).
 * Nothing else in the app sets or reads this param, so an organic visit never sees it.
 */
export function useOnboardingGuide(stepId: string): ComputedRef<boolean> {
  const route = useRoute();

  return computed(() => route.query.onboarding === stepId);
}
