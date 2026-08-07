<!--
  A presentational icon, nothing more. Menu.vue wraps it in its own <button @click="handleLogout">,
  so this component neither handles clicks nor navigates.

  It previously carried an `onClick` method declared as a *root* option rather than inside
  `methods`. Vue ignores unknown root options, so it never ran — dead code that read as if this
  component logged you out. Removed rather than moved into `methods`, because the parent already
  owns that behaviour and having it in both places would log out twice.
-->
<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    xmlns="http://www.w3.org/2000/svg"
    role="img"
    :aria-label="ariaLabel"
    :style="{ color }"
  >
    <!-- "door" -->
    <path fill="currentColor" d="M11 3h8a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-8v-2h8V5h-8V3z" />
    <!-- "arrow exit" (flipped to point left) -->
    <path fill="currentColor" d="M4 12l4 4v-3h6v-2H8V8l-4 4z" />
  </svg>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    /** Width and height in px. */
    size?: number | string;
    color?: string;
    ariaLabel?: string;
  }>(),
  { size: 24, color: 'currentColor', ariaLabel: 'Logout' },
);
</script>
