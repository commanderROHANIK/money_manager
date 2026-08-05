<template>
    <StatCard label="Total Balance" :value="formattedBalance" />
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchBankAccountsTotalBalance } from '../../../services/api'; // Adjust the import path as necessary
import StatCard from '../../ui/StatCard.vue';

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
