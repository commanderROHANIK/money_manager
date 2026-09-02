import { computed } from 'vue';
import type { ComputedRef } from 'vue';
import { useRoute, useRouter } from 'vue-router';

export interface OnboardingGuide {
  /**
   * True when this page was reached via the onboarding checklist's guided "Go" for `stepId` —
   * carried as a `?onboarding=<stepId>` query param (see `resolveTo` in `useOnboarding.ts`).
   * Nothing else in the app sets or reads this param, so an organic visit never sees it.
   */
  active: ComputedRef<boolean>;
  /**
   * Drops the `onboarding` param once the guided action actually succeeds. Without this the
   * banner/highlight has nothing to turn it off again: `active` is derived purely from the URL,
   * and nothing else in the app ever changes that URL, so a landlord who completes a guided step
   * would otherwise keep being told to do the thing they just did until they navigated away and
   * back. A no-op when this step isn't the active one, so a page with several guided widgets
   * (`PropertyDetailPage.vue`) can call every step's `clear()` after any one create and only the
   * one actually matching the URL does anything.
   */
  clear: () => void;
}

export function useOnboardingGuide(stepId: string): OnboardingGuide {
  const route = useRoute();
  const router = useRouter();

  const active = computed(() => route.query.onboarding === stepId);

  function clear(): void {
    if (!active.value) return;

    const { onboarding: _onboarding, ...rest } = route.query;
    void router.replace({ query: rest });
  }

  return { active, clear };
}
