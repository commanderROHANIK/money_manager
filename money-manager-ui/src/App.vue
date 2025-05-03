<template>
  <div class="min-h-screen bg-gray-100">
    <UpcomingEvents />
    <BankAccounts />
    <LoanPage />
  </div>
</template>

<script setup lang="ts">
import BankAccounts from './components/BankAccounts.vue';
import LoanPage from './components/LoanPage.vue';
import UpcomingEvents from './components/UpcomingEvents.vue'
</script>
