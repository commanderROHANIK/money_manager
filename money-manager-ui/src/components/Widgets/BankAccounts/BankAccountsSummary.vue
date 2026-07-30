<template>
    <StatCard label="Bank Accounts">
      <template #value>{{ accountCount }} Accounts Connected</template>
    </StatCard>
  </template>

  <script setup lang="ts">
  import { ref, onMounted, computed } from 'vue';
  import { fetchBankAccounts } from '../../../services/api';
  import type { BankAccount } from '../../../models/models';
  import StatCard from '../../ui/StatCard.vue';

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
