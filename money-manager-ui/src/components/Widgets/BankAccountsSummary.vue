<template>
    <div class="bg-white p-6 rounded-2xl shadow-md">
      <h2 class="text-lg font-semibold mb-2">Bank Accounts</h2>
      <p class="text-sm">{{ accountCount }} Accounts Connected</p>
    </div>
  </template>
  
  <script setup lang="ts">
  import { ref, onMounted, computed } from 'vue';
  import { fetchBankAccounts } from '../../services/api';
  import type { BankAccount } from '../../models/models';
  
  const accounts = ref<BankAccount[]>([]);
  
  onMounted(async () => {
    try {
      accounts.value = await fetchBankAccounts();
    } catch (error) {
      console.error('Failed to load bank accounts:', error);
    }
  });
  
  const accountCount = computed(() => accounts.value.length);
  </script>
  