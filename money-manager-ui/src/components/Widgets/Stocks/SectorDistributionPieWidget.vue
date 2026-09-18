<template>
  <div>
    <div v-if="loading" class="text-sm text-text-muted">Loading...</div>
    <div v-else-if="!hasData">
      <p class="text-center text-sm text-text-muted">No data available to display sector distribution.</p>
    </div>
    <PieChart v-else :segments="segments" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchStocks } from '../../../services/api';
import type { Stock } from '../../../models/models';
import { chartCategoricalPalette } from '../../../utils/chartTheme';
import PieChart from '../../ui/PieChart.vue';

const stocks = ref<Stock[]>([]);
const loading = ref(true);

onMounted(async () => {
  try {
    stocks.value = await fetchStocks();
  } finally {
    loading.value = false;
  }
});

// Stub sector classification
function getSector(ticker: string): string {
  if (ticker.startsWith('A')) return 'Technology';
  if (ticker.startsWith('B')) return 'Finance';
  if (ticker.startsWith('C')) return 'Energy';
  return 'Other';
}

const sectorDistribution = computed(() => {
  const result: Record<string, number> = {};

  for (const stock of stocks.value) {
    const sector = getSector(stock.ticker);
    const value = stock.currentPrice * stock.sharesOwned;
    result[sector] = (result[sector] || 0) + value;
  }

  return result;
});

const hasData = computed(() => Object.keys(sectorDistribution.value).length > 0);

const segments = computed(() => {
  const palette = chartCategoricalPalette();
  return Object.entries(sectorDistribution.value).map(([label, value], index) => ({
    label,
    value,
    color: palette[index % palette.length],
  }));
});
</script>
