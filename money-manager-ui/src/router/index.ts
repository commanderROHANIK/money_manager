// src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router';
import Dashboard from '../components/Dashboard.vue';
import AccountsView from '../components/BankAccounts.vue';
import LoansView from '../components/LoanPage.vue';
import PropertiesView from '../components/RentalPropertyPage.vue';
import StocksView from '../components/StockPage.vue';
import EventsView from '../components/UpcomingEvents.vue';

import Login from '../components/Login.vue';
import Register from '../components/Register.vue';

import { isLoggedIn } from '../services/authService';

const routes = [
  { path: '/', name: 'Dashboard', component: Dashboard, meta: { requiresAuth: true } },
  { path: '/accounts', name: 'Accounts', component: AccountsView, meta: { requiresAuth: true } },
  { path: '/loans', name: 'Loans', component: LoansView, meta: { requiresAuth: true } },
  { path: '/properties', name: 'Properties', component: PropertiesView, meta: { requiresAuth: true } },
  { path: '/stocks', name: 'Stocks', component: StocksView, meta: { requiresAuth: true } },
  { path: '/events', name: 'Events', component: EventsView, meta: { requiresAuth: true } },

  { path: '/login', name: 'Login', component: Login },
  { path: '/register', name: 'Register', component: Register }
];

const router = createRouter({
  history: createWebHistory(),
  routes
});

// Navigation guard for auth
router.beforeEach((to, _, next) => {
  if (to.meta.requiresAuth && !isLoggedIn()) {
    next('/login');
  } else if ((to.path === '/login' || to.path === '/register') && isLoggedIn()) {
    next('/');
  } else {
    next();
  }
});

export default router;
