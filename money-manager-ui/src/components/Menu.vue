<template>
    <aside class="flex w-56 flex-col gap-1 bg-sidebar-bg p-3.5 text-sidebar-text" :hidden="loggedIn">
      <div class="mb-2 flex items-center gap-2 px-2.5 py-2 font-heading text-base font-extrabold">
        <span class="h-5.5 w-5.5 rounded-sm bg-primary"></span>
        <h1>Money Manager</h1>
      </div>
      <nav class="flex flex-1 flex-col gap-1 text-sm font-semibold">
        <RouterLink to="/" class="rounded-md px-3.5 py-2.5 opacity-75 hover:opacity-100" active-class="bg-white/12 opacity-100">Dashboard</RouterLink>
        <RouterLink to="/accounts" class="rounded-md px-3.5 py-2.5 opacity-75 hover:opacity-100" active-class="bg-white/12 opacity-100">Accounts</RouterLink>
        <RouterLink to="/loans" class="rounded-md px-3.5 py-2.5 opacity-75 hover:opacity-100" active-class="bg-white/12 opacity-100">Loans</RouterLink>
        <RouterLink to="/properties" class="rounded-md px-3.5 py-2.5 opacity-75 hover:opacity-100" active-class="bg-white/12 opacity-100">Properties</RouterLink>
        <RouterLink to="/stocks" class="rounded-md px-3.5 py-2.5 opacity-75 hover:opacity-100" active-class="bg-white/12 opacity-100">Stocks</RouterLink>
        <RouterLink to="/events" class="rounded-md px-3.5 py-2.5 opacity-75 hover:opacity-100" active-class="bg-white/12 opacity-100">Events</RouterLink>
      </nav>
      <button class="mt-auto flex items-center gap-2 rounded-md px-3.5 py-2.5 text-sm opacity-60 hover:opacity-100" @click="handleLogout">
        <Logout :size="18" />
        Log out
      </button>
    </aside>
  </template>
  
  <script setup lang="ts">
  import { RouterLink } from 'vue-router';
  import Logout from '../components/LogoutIcon.vue';
  import { isLoggedIn, logout } from '../services/authService';

  const loggedIn = !isLoggedIn();

  function handleLogout() {
    logout();
    window.location.reload();
  }
  </script>
  