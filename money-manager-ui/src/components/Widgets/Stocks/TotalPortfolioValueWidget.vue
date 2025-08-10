<template>
  <div class="bg-white p-6 rounded-2xl shadow-md">
    <h2 class="text-lg font-semibold mb-2">Total Portfolio Value</h2>
    <p class="text-2xl font-bold text-green-600">
      {{ formattedValue }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchStocks } from '../../../services/api';
import type { Stock } from '../../../models/models';

const stocks = ref<Stock[]>([]);

onMounted(async () => {
  try {
    stocks.value = await fetchStocks();
  } catch (error) {
    console.error('Failed to fetch stocks:', error);
  }
});

const totalValue = computed(() => {
  return stocks.value.reduce((sum, stock) => {
    return sum + stock.sharesOwned * stock.currentPrice;
  }, 0);
});

const formattedValue = computed(() => {
  return new Intl.NumberFormat('hu-HU', {
    style: 'currency',
    currency: 'HUF',
    maximumFractionDigits: 0,
  }).format(totalValue.value);
});
</script>
