<template>
  <div ref="el" :class="active ? 'rounded-lg ring-2 ring-primary-strong ring-offset-2 ring-offset-surface' : ''">
    <p
      v-if="active"
      class="mb-3 rounded-md bg-primary-strong/10 px-3 py-2 text-sm text-primary-strong"
    >
      {{ t(`onboarding.guide.${stepId}`) }}
    </p>
    <slot />
  </div>
</template>

<script setup lang="ts">
/**
 * Wraps a widget the onboarding checklist can guide a landlord to. Reuses the widget everyone
 * already sees — this is the "deep-link + highlight" half of guided onboarding, not a separate
 * flow — and only changes anything when arrived via the checklist's "Go" (see
 * `useOnboardingGuide`): scrolls itself into view and shows a short explanation above the widget.
 * An organic visit renders exactly as before.
 */
import { nextTick, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useOnboardingGuide } from '../../composables/useOnboardingGuide';

const props = defineProps<{ stepId: string }>();
const { t } = useI18n();
const { active } = useOnboardingGuide(props.stepId);
const el = ref<HTMLElement | null>(null);

// Watched rather than a one-shot onMounted: `active` is reactive (it tracks the route), so if it
// ever turns true without this component remounting — not reachable today, since the only
// producer of `?onboarding=` lives on the dashboard and every "Go" is a fresh navigation to a
// different route, but not something this component should rely on staying true — the scroll
// still fires. `immediate: true` covers the ordinary case, arriving already guided.
watch(
  active,
  async (isActive) => {
    if (!isActive) return;

    // The card it lives in may still be settling its layout (data just arrived); wait a tick so
    // scrollIntoView targets its final position rather than one about to shift.
    await nextTick();
    el.value?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  },
  { immediate: true }
);
</script>
