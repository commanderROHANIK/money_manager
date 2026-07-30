<template>
  <div class="flex flex-col items-center gap-2.5 rounded-lg border border-dashed border-border bg-surface p-7 text-center">
    <div v-if="$slots.icon" class="flex h-11 w-11 items-center justify-center rounded-md bg-primary-soft">
      <slot name="icon" />
    </div>
    <div class="text-sm font-bold">{{ title }}</div>
    <div v-if="description" class="text-sm text-text-muted">{{ description }}</div>
    <div v-if="$slots.actions" class="mt-1.5">
      <slot name="actions" />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{ title: string; description?: string }>();
</script>
