<template>
  <div class="flex flex-col items-center gap-2.5 rounded-lg border border-danger bg-danger-soft p-7 text-center">
    <div class="text-sm font-bold text-danger">{{ title }}</div>
    <div v-if="description" class="text-sm text-text/80">{{ description }}</div>
    <div v-if="$slots.actions" class="mt-1.5">
      <slot name="actions" />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{ title: string; description?: string }>();
</script>
