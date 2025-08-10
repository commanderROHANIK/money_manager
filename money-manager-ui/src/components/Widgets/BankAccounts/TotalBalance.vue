<template>
    <div class="bg-white p-6 rounded-2xl shadow-md">
        <h2 class="text-lg font-semibold mb-2">Total Balance</h2>
        <p class="text-2xl font-bold text-green-600">
            {{ formattedBalance  }}
        </p>
    </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchBankAccountsTotalBalance } from '../../../services/api'; // Adjust the import path as necessary

const balance = ref<number | null>(null);

onMounted(async () => {
  try {
    balance.value = await fetchBankAccountsTotalBalance();
  } catch (err) {
    console.error('Failed to fetch balance:', err);
  }
});

const formattedBalance = computed(() => {
  if (balance.value === null) return '';
  return new Intl.NumberFormat('hu-HU', {
    style: 'currency',
    currency: 'HUF',
    maximumFractionDigits: 0,
  }).format(balance.value);
});
</script>
