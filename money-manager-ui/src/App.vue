<template>
  <div class="min-h-screen bg-gray-100">
    <UpcomingEvents />
    <BankAccounts />
    <LoanPage />
    <RentalPropertyPage />
  </div>
</template>

<script setup lang="ts">
import BankAccounts from './components/BankAccounts.vue';
import LoanPage from './components/LoanPage.vue';
import RentalPropertyPage from './components/RentalPropertyPage.vue';
import UpcomingEvents from './components/UpcomingEvents.vue'
</script>
