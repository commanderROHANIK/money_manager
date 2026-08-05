<template>
  <div class="flex flex-col gap-1.5 rounded-lg border border-border bg-surface p-4 shadow-card">
    <span class="text-xs font-semibold text-text-muted">{{ label }}</span>
    <span class="font-heading text-xl font-extrabold tabular-nums">
      <slot name="value">{{ value }}</slot>
    </span>
    <span v-if="delta" class="text-xs font-bold" :class="deltaPositive ? 'text-primary' : 'text-danger'">
      {{ delta }}
    </span>
  </div>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    label: string;
    value?: string | number;
    delta?: string;
    deltaPositive?: boolean;
  }>(),
  { deltaPositive: true },
);
</script>
