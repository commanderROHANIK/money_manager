<template>
  <div class="flex flex-col gap-3 rounded-lg border border-border bg-surface p-6">
    <div
      v-for="i in rows"
      :key="i"
      class="h-4 rounded-sm"
      :style="{
        width: widths[(i - 1) % widths.length],
        backgroundImage:
          'linear-gradient(90deg, var(--color-surface-2) 25%, var(--color-border) 37%, var(--color-surface-2) 63%)',
        backgroundSize: '400% 100%',
        animation: 'mm-shimmer 1.4s ease infinite',
      }"
    />
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{ rows?: number }>(), { rows: 3 });

const widths = ['60%', '80%', '40%'];
</script>
