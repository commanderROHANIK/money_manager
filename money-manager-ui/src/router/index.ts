// src/router.ts or src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router';
import Dashboard from '../components/Dashboard.vue'; // Adjust the path as necessary
import AccountsView from '../components/BankAccounts.vue';
import LoansView from '../components/LoanPage.vue';
import PropertiesView from '../components/RentalPropertyPage.vue';
import StocksView from '../components/StockPage.vue';
import EventsView from '../components/UpcomingEvents.vue';

const routes = [
  { path: '/', name: 'Dashboard', component: Dashboard },
  { path: '/accounts', name: 'Accounts', component: AccountsView },
  { path: '/loans', name: 'Loans', component: LoansView },
  { path: '/properties', name: 'Properties', component: PropertiesView },
  { path: '/stocks', name: 'Stocks', component: StocksView },
  { path: '/events', name: 'Events', component: EventsView },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

export default router;
