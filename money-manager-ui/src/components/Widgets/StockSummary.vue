<template>
    <div class="bg-white p-6 rounded-2xl shadow-md">
      <h2 class="text-lg font-semibold mb-2">Stock Overview</h2>
  
      <div v-if="stocks.length > 0" class="space-y-1 text-sm">
        <div v-for="stock in stocks" :key="stock.id" class="flex justify-between">
          <span>{{ stock.ticker }} ({{ stock.sharesOwned }}x)</span>
          <span>{{ formatCurrency(stock.sharesOwned * stock.currentPrice) }}</span>
        </div>
  
        <div class="mt-3 font-semibold">
          Total Value: {{ formatCurrency(totalValue) }}
        </div>
        <div class="text-green-600 text-sm">
          📈 +{{ gainPercent.toFixed(1) }}% this month
        </div>
      </div>
  
      <p v-else class="text-gray-500 text-sm">No stock data available.</p>
    </div>
  </template>
  
  <script setup lang="ts">
  import { ref, onMounted, computed } from 'vue';
  import { fetchStocks } from '../../services/api';
  import type { Stock } from '../../models/models';
  
  const stocks = ref<Stock[]>([]);
  
  onMounted(async () => {
    try {
      stocks.value = await fetchStocks();
    } catch (error) {
      console.error('Failed to load stocks:', error);
    }
  });
  
  const totalValue = computed(() =>
    stocks.value.reduce((sum, s) => sum + s.currentPrice * s.sharesOwned, 0)
  );
  
  // Optional: Dummy % gain (can be replaced by backend later)
  const totalCost = computed(() =>
    stocks.value.reduce((sum, s) => sum + s.purchasePrice * s.sharesOwned, 0)
  );
  
  const gainPercent = computed(() =>
    totalCost.value > 0 ? ((totalValue.value - totalCost.value) / totalCost.value) * 100 : 0
  );
  
  function formatCurrency(value: number): string {
    return new Intl.NumberFormat('hu-HU', {
      style: 'currency',
      currency: 'HUF',
      maximumFractionDigits: 0
    }).format(value);
  }
  </script>
  