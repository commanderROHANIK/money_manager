<template>
  <div class="flex h-screen">
    <Menu />
    <main class="flex-1 overflow-y-auto bg-gray-100">
      <router-view />
    </main>
  </div>
</template>

<script setup lang="ts">
import Menu from './components/Dashboard/Menu.vue'; // Adjust path if needed
</script>
