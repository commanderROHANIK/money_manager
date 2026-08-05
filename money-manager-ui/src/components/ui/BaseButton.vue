<template>
  <button
    :type="type"
    :disabled="disabled"
    class="inline-flex items-center justify-center gap-2 rounded-md text-sm font-bold transition disabled:cursor-not-allowed disabled:opacity-50"
    :class="[sizeClass, variantClass, block ? 'w-full' : '']"
  >
    <slot />
  </button>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
    size?: 'default' | 'sm';
    type?: 'button' | 'submit';
    disabled?: boolean;
    block?: boolean;
  }>(),
  {
    variant: 'primary',
    size: 'default',
    type: 'button',
    disabled: false,
    block: false,
  },
);

const sizeClass = computed(() =>
  props.size === 'sm' ? 'px-4 py-2 text-xs' : 'px-5 py-3',
);

// Filled primary sits on --primary-strong rather than --primary: white on --primary measures
// 4.02:1, under the 4.5:1 AA floor for this 14px bold label. --primary stays the brand fill
// elsewhere (logo marks, chart series, the active nav highlight).
const variantClass = computed(() => ({
  primary: 'bg-primary-strong text-white hover:bg-primary-pressed',
  secondary: 'border border-border bg-surface text-text hover:bg-surface-2',
  ghost: 'bg-transparent text-primary-strong hover:bg-primary-soft',
  danger: 'bg-danger-soft text-danger-strong hover:bg-danger/15',
}[props.variant]));
</script>
