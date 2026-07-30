<template>
  <span
    class="inline-flex items-center whitespace-nowrap rounded-full px-3 py-1 text-xs font-bold"
    :class="[variantClass, outline ? 'font-mono' : '']"
  >
    <slot />
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'accent' | 'danger' | 'neutral' | 'outline';
  }>(),
  { variant: 'neutral' },
);

const outline = computed(() => props.variant === 'outline');

const variantClass = computed(() => ({
  primary: 'bg-primary-soft text-primary-strong',
  accent: 'bg-accent-soft text-accent',
  danger: 'bg-danger-soft text-danger',
  neutral: 'bg-surface-2 text-text-muted',
  outline: 'border border-border text-text',
}[props.variant]));
</script>
