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

const variantClass = computed(() => ({
  primary: 'bg-primary text-white hover:bg-primary-strong',
  secondary: 'border border-border bg-surface text-text hover:bg-surface-2',
  ghost: 'bg-transparent text-primary hover:bg-primary-soft',
  danger: 'bg-danger-soft text-danger hover:bg-danger/15',
}[props.variant]));
</script>
