<template>
  <div class="flex h-screen">
    <Menu />
    <main class="flex-1 overflow-y-auto bg-bg">
      <router-view />
    </main>
  </div>
</template>

<script setup lang="ts">
  import Menu from './components/Menu.vue'; 
</script>
