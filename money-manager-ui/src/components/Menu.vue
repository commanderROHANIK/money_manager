<template>
    <aside class="flex w-56 flex-col gap-1 bg-sidebar-bg p-3.5 text-sidebar-text" :hidden="loggedIn">
      <div class="mb-2 flex items-center gap-2 px-2.5 py-2 font-heading text-base font-extrabold">
        <span class="h-5.5 w-5.5 rounded-sm bg-primary"></span>
        <h1>Money Manager</h1>
      </div>
      <nav class="flex flex-1 flex-col gap-1 text-sm font-semibold">
        <RouterLink
          v-for="link in links"
          :key="link.to"
          :to="link.to"
          class="rounded-md px-3.5 py-2.5 hover:opacity-100"
          :class="isActive(link.to) ? 'bg-white/12 opacity-100' : 'opacity-75'"
        >
          {{ link.label }}
        </RouterLink>
      </nav>
      <button class="mt-auto flex items-center gap-2 rounded-md px-3.5 py-2.5 text-sm opacity-60 hover:opacity-100" @click="handleLogout">
        <Logout :size="18" />
        Log out
      </button>
    </aside>
  </template>
  
  <script setup lang="ts">
  import { RouterLink, useRoute } from 'vue-router';
  import Logout from '../components/LogoutIcon.vue';
  import { isLoggedIn, logout } from '../services/authService';

  const loggedIn = !isLoggedIn();
  const route = useRoute();

  const links = [
    { to: '/', label: 'Dashboard' },
    { to: '/accounts', label: 'Accounts' },
    { to: '/loans', label: 'Loans' },
    { to: '/properties', label: 'Properties' },
    { to: '/stocks', label: 'Stocks' },
    { to: '/events', label: 'Events' },
  ];

  // RouterLink's own active-class matches on route records, and the routes are flat, so
  // /properties/:id would leave the Properties item unhighlighted. Matching on the path prefix
  // keeps the section marked while you are on a detail page.
  function isActive(to: string): boolean {
    return to === '/' ? route.path === '/' : route.path === to || route.path.startsWith(to + '/');
  }

  function handleLogout() {
    logout();
    window.location.reload();
  }
  </script>
  
