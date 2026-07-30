<template>
  <div class="rounded-lg border border-border bg-surface shadow-card" :class="padded ? 'p-5' : ''">
    <div v-if="title || $slots.actions" class="mb-3.5 flex items-center justify-between">
      <h2 v-if="title" class="font-heading text-base font-bold">{{ title }}</h2>
      <slot name="actions" />
    </div>
    <slot />
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{ title?: string; padded?: boolean }>(), { padded: true });
</script>
