<template>
    <div class="flex h-screen">
      <!-- Sidebar -->
        <Menu />
  
      <!-- Main content -->
      <main class="flex-1 bg-gray-100 p-6 overflow-y-auto">
        <h1 class="text-2xl font-semibold mb-6">Dashboard</h1>
  
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
      </main>
    </div>
  </template>
  
  <script setup lang="ts">
    import Menu from './Dashboard/Menu.vue';
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
  