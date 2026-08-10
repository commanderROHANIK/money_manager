<template>
    <aside class="flex w-56 flex-col gap-1 bg-sidebar-bg p-3.5 text-sidebar-text" :hidden="loggedIn">
      <div class="mb-2 flex items-center gap-2 px-2.5 py-2 font-heading text-base font-extrabold">
        <span class="h-5.5 w-5.5 rounded-sm bg-primary"></span>
        <h1>{{ t('app.name') }}</h1>
      </div>
      <nav class="flex flex-1 flex-col gap-1 text-sm font-semibold">
        <RouterLink
          v-for="link in visibleLinks"
          :key="link.to"
          :to="link.to"
          class="rounded-md px-3.5 py-2.5 hover:opacity-100"
          :class="isActive(link.to) ? 'bg-white/12 opacity-100' : 'opacity-75'"
        >
          {{ t(link.label) }}
        </RouterLink>
      </nav>
      <button class="mt-auto flex items-center gap-2 rounded-md px-3.5 py-2.5 text-sm opacity-60 hover:opacity-100" @click="handleLogout">
        <Logout :size="18" />
        {{ t('nav.logout') }}
      </button>
    </aside>
  </template>
  
  <script setup lang="ts">
  import { computed } from 'vue';
  import { useI18n } from 'vue-i18n';
  import { RouterLink, useRoute } from 'vue-router';
  import Logout from '../components/LogoutIcon.vue';
  import { isLoggedIn, logout } from '../services/authService';
  import { featureFlags } from '../services/features';
  import type { FeatureName } from '../services/features';

  const { t } = useI18n();
  const loggedIn = !isLoggedIn();
  const route = useRoute();

  // `feature` names the section a link belongs to; a link without one is part of the core
  // product and is always shown. The names have to match the route metadata in router/index.ts,
  // which is what makes a hidden link and a refused route the same decision.
  //
  // `label` is a translation key rather than text, resolved in the template, so the sidebar
  // follows a language change without the list being rebuilt.
  const links: { to: string; label: string; feature?: FeatureName }[] = [
    { to: '/', label: 'nav.dashboard' },
    { to: '/accounts', label: 'nav.accounts', feature: 'banking' },
    { to: '/loans', label: 'nav.loans', feature: 'loans' },
    { to: '/properties', label: 'nav.properties' },
    { to: '/stocks', label: 'nav.stocks', feature: 'stocks' },
    { to: '/events', label: 'nav.events', feature: 'events' },
    { to: '/settings', label: 'nav.settings' },
  ];

  const visibleLinks = computed(() =>
    links.filter((link) => !link.feature || featureFlags.value[link.feature])
  );

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
  
