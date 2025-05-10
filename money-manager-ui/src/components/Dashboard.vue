<template>
  
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <!-- Total Balance Widget -->
          <TotalBalance />
  
          <!-- Upcoming Events Widget -->
          <UpcomingEvents />
  
          <!-- Stock Summary Widget -->
          <StockSummary />
  
          <!-- Loan Summary Widget -->
          <LoanSummary />
  
          <!-- Property Summary Widget -->
          <PropertySummary />
  
          <!-- Bank Account Summary Widget -->
          <BankAccountsSummary />
        </div>
  </template>
  
  <script setup lang="ts">
    import UpcomingEvents from './Widgets/UpcomingEvents.vue';
    import TotalBalance from './Widgets/TotalBalance.vue';
    import StockSummary from './Widgets/StockSummary.vue';
    import LoanSummary from './Widgets/LoanSummary.vue';
    import PropertySummary from './Widgets/PropertySummary.vue';
    import BankAccountsSummary from './Widgets/BankAccountsSummary.vue';
  </script>
  
  <style scoped>
  /* Optional scoped styling if needed */
  </style>
  