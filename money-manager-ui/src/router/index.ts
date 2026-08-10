// src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router';
import Dashboard from '../components/Dashboard.vue';
import AccountsView from '../components/BankAccounts.vue';
import LoansView from '../components/LoanPage.vue';
import PropertiesView from '../components/RentalPropertyPage.vue';
import PropertyDetailView from '../components/PropertyDetailPage.vue';
import StocksView from '../components/StockPage.vue';
import EventsView from '../components/UpcomingEvents.vue';
import SettingsView from '../components/SettingsPage.vue';

import Login from '../components/Login.vue';
import Register from '../components/Register.vue';

import { isLoggedIn } from '../services/authService';
import { ensureFeaturesLoaded } from '../services/features';
import type { FeatureName } from '../services/features';

declare module 'vue-router' {
  interface RouteMeta {
    requiresAuth?: boolean;
    /** The section this route belongs to. Absent means the route is part of the core product. */
    feature?: FeatureName;
  }
}

const routes = [
  { path: '/', name: 'Dashboard', component: Dashboard, meta: { requiresAuth: true } },
  {
    path: '/accounts',
    name: 'Accounts',
    component: AccountsView,
    meta: { requiresAuth: true, feature: 'banking' as const },
  },
  {
    path: '/loans',
    name: 'Loans',
    component: LoansView,
    meta: { requiresAuth: true, feature: 'loans' as const },
  },
  { path: '/properties', name: 'Properties', component: PropertiesView, meta: { requiresAuth: true } },
  {
    path: '/properties/:id',
    name: 'PropertyDetail',
    component: PropertyDetailView,
    meta: { requiresAuth: true },
  },
  {
    path: '/stocks',
    name: 'Stocks',
    component: StocksView,
    meta: { requiresAuth: true, feature: 'stocks' as const },
  },
  {
    path: '/events',
    name: 'Events',
    component: EventsView,
    meta: { requiresAuth: true, feature: 'events' as const },
  },
  { path: '/settings', name: 'Settings', component: SettingsView, meta: { requiresAuth: true } },

  { path: '/login', name: 'Login', component: Login },
  { path: '/register', name: 'Register', component: Register }
];

const router = createRouter({
  history: createWebHistory(),
  routes
});

// Navigation guard for auth, and for the sections this deployment does not present.
//
// The feature flags are awaited for every authenticated route rather than only for the gated
// ones, because the navigation is rendered from them: resolving them lazily would paint the
// sidebar with links that vanish a moment later, which is precisely what switching a section off
// is meant to prevent. It costs one small request before the first authenticated view, shared by
// everything that reads the flags afterwards.
router.beforeEach(async (to, _, next) => {
  if (to.meta.requiresAuth && !isLoggedIn()) {
    next('/login');
    return;
  }

  if ((to.path === '/login' || to.path === '/register') && isLoggedIn()) {
    next('/');
    return;
  }

  if (!to.meta.requiresAuth) {
    next();
    return;
  }

  const features = await ensureFeaturesLoaded();
  const feature = to.meta.feature;

  // A bookmarked or typed URL for a switched-off section. Redirecting to the dashboard rather
  // than rendering it: the view would mount, fire its requests, and fill with the 404s the API
  // now answers — a broken page instead of an absent one.
  if (feature && !features[feature]) {
    next('/');
    return;
  }

  next();
});

export default router;
