<template>
  <component :is="as" class="flex items-center justify-between gap-3 border-b border-border py-3.5 last:border-none">
    <div class="flex min-w-0 flex-col gap-0.5">
      <slot name="title" />
      <slot name="subtitle" />
    </div>
    <div class="flex items-center gap-3 whitespace-nowrap">
      <slot name="trailing" />
    </div>
  </component>
</template>

<script setup lang="ts">
withDefaults(defineProps<{ as?: string }>(), { as: 'li' });
</script>
