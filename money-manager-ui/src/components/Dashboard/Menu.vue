<template>
    <aside class="w-64 bg-gray-900 text-white flex flex-col">
      <div class="p-6 text-xl font-bold border-b border-gray-700">Money Manager</div>
      <nav class="flex-1 p-4 space-y-4">
        <RouterLink to="/" class="block py-2 px-4 rounded hover:bg-gray-800">Dashboard</RouterLink>
        <RouterLink to="/accounts" class="block py-2 px-4 rounded hover:bg-gray-800">Accounts</RouterLink>
        <RouterLink to="/loans" class="block py-2 px-4 rounded hover:bg-gray-800">Loans</RouterLink>
        <RouterLink to="/properties" class="block py-2 px-4 rounded hover:bg-gray-800">Properties</RouterLink>
        <RouterLink to="/stocks" class="block py-2 px-4 rounded hover:bg-gray-800">Stocks</RouterLink>
        <RouterLink to="/events" class="block py-2 px-4 rounded hover:bg-gray-800">Events</RouterLink>
      </nav>
    </aside>
  </template>
  
  <script setup lang="ts">
  import { RouterLink } from 'vue-router';
  </script>
  