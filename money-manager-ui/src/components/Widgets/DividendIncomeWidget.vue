<template>
  <div class="text-2xl font-bold text-green-600">
    {{ formattedDividend }}
  </div>
</template>

<script lang="ts" setup>
import { ref, computed, onMounted } from 'vue';
import { fetchStocks } from '../../services/api';
import type { Stock } from '../../models/models';

const annualDividend = ref(0);

onMounted(async () => {
  const stocks = await fetchStocks();

  // Estimate 2% dividend yield on current value
  annualDividend.value = stocks.reduce((total, stock) => {
    const value = stock.currentPrice * stock.sharesOwned;
    return total + value * 0.02;
  }, 0);
});

const formattedDividend = computed(() =>
  annualDividend.value.toLocaleString('hu-HU', {
    style: 'currency',
    currency: 'HUF',
    maximumFractionDigits: 0,
  })
);
</script>
