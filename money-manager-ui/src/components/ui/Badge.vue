<template>
  <span
    class="inline-flex w-fit items-center whitespace-nowrap rounded-full px-3 py-1 text-xs font-bold"
    :class="[variantClass, mono ? 'font-mono' : '']"
  >
    <slot />
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'accent' | 'danger' | 'neutral' | 'outline';
    // Tabular content (tickers, codes) reads better monospaced. Kept separate from `variant`
    // so picking a look does not silently change the typeface.
    mono?: boolean;
  }>(),
  { variant: 'neutral', mono: false },
);

const variantClass = computed(() => ({
  primary: 'bg-primary-soft text-primary-strong',
  accent: 'bg-accent-soft text-accent-strong',
  danger: 'bg-danger-soft text-danger-strong',
  neutral: 'bg-surface-2 text-text-muted',
  outline: 'border border-border text-text',
}[props.variant]));
</script>
