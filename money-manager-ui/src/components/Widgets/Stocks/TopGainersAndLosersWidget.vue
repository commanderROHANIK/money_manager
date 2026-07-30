<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import type { Stock } from '../../../models/models';
import { fetchStocks } from '../../../services/api';
import BaseCard from '../../ui/BaseCard.vue';

const stocks = ref<Stock[]>([]);
const isLoading = ref(true);

onMounted(async () => {
  stocks.value = await fetchStocks();
  isLoading.value = false;
});

const sortedStocks = computed(() => {
  return [...stocks.value].sort((a, b) => {
    const perfA = ((a.currentPrice - a.purchasePrice) / a.purchasePrice) * 100;
    const perfB = ((b.currentPrice - b.purchasePrice) / b.purchasePrice) * 100;
    return perfB - perfA;
  });
});

const topGainers = computed(() => sortedStocks.value.slice(0, 3));
const topLosers = computed(() => sortedStocks.value.slice(-3).reverse());
</script>

<template>
  <BaseCard title="Top Gainers & Losers">
    <div v-if="isLoading" class="text-sm text-text-muted">Loading...</div>

    <div v-else class="grid grid-cols-2 gap-4">
      <div>
        <h3 class="font-heading text-sm font-bold text-text-muted mb-1">Top Gainers</h3>
        <ul>
          <li v-for="stock in topGainers" :key="stock.id" class="text-primary text-sm tabular-nums">
            {{ stock.ticker }} +{{ ((stock.currentPrice - stock.purchasePrice) / stock.purchasePrice * 100).toFixed(2) }}%
          </li>
        </ul>
      </div>

      <div>
        <h3 class="font-heading text-sm font-bold text-text-muted mb-1">Top Losers</h3>
        <ul>
          <li v-for="stock in topLosers" :key="stock.id" class="text-danger text-sm tabular-nums">
            {{ stock.ticker }} {{ ((stock.currentPrice - stock.purchasePrice) / stock.purchasePrice * 100).toFixed(2) }}%
          </li>
        </ul>
      </div>
    </div>
  </BaseCard>
</template>

<style scoped>
ul {
  list-style-type: disc;
  padding-left: 1.25rem;
}
</style>
