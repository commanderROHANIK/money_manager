<template>

        <div class="p-4 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <!-- Total Balance Widget -->
          <TotalBalance v-if="featureFlags.banking" />

          <!-- Upcoming Events Widget -->
          <UpcomingEvents v-if="featureFlags.events" />

          <!-- Stock Summary Widget -->
          <StockSummary v-if="featureFlags.stocks" />

          <!-- Loan Summary Widget -->
          <LoanSummary v-if="featureFlags.loans" />

          <!-- Property Summary Widget -->
          <PropertySummary />

          <!-- Bank Account Summary Widget -->
          <BankAccountsSummary v-if="featureFlags.banking" />
        </div>
  </template>

  <script setup lang="ts">
    import UpcomingEvents from './Widgets/Events/UpcomingEvents.vue';
    import TotalBalance from './Widgets/BankAccounts/TotalBalance.vue';
    import StockSummary from './Widgets/Stocks/StockSummary.vue';
    import LoanSummary from './Widgets/Loans/LoanSummary.vue';
    import PropertySummary from './Widgets/Properties/PropertySummary.vue';
    import BankAccountsSummary from './Widgets/BankAccounts/BankAccountsSummary.vue';
    import { featureFlags } from '../services/features';

    // v-if rather than v-show: a hidden widget still mounts, and mounting it fires the request
    // its section's endpoints now answer 404. The widget has to not exist, not merely not show.
  </script>

  <style scoped>
  /* Optional scoped styling if needed */
  </style>
