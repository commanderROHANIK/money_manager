<template>
  <div>
    <div v-if="loading" class="text-sm text-text-muted">Loading...</div>
    <div v-else-if="!hasData">
      <p class="text-center text-sm text-text-muted">No data available to display sector distribution.</p>
    </div>
    <template v-else>
      <PieChart :segments="segments">
        <template #center>
          <span class="font-heading text-xs font-extrabold tabular-nums leading-tight">{{ formattedTotal }}</span>
          <span class="text-[9px] font-semibold text-text-muted uppercase tracking-wide">Total</span>
        </template>
      </PieChart>
      <p v-if="missingRateMessage" class="text-xs text-accent-strong mt-2">{{ missingRateMessage }}</p>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchStocks, fetchStocksTotalValue } from '../../../services/api';
import type { Stock, StockValueSummary } from '../../../models/models';
import { chartCategoricalPalette } from '../../../utils/chartTheme';
import { formatMoney } from '../../../utils/money';
import { useRateDisclosure } from '../../../composables/useRateDisclosure';
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

// NOTE: this buckets `currentPrice * sharesOwned` with no reference to `stock.currencyCode` — a
// pre-existing issue, not introduced here. If holdings span currencies, these bucket values (and
// therefore the ring's own proportions) can be wrong the same way the center total used to be
// before it was wired to the rollup below. Fixing that needs a per-holding converted breakdown
// from the backend, which is a bigger change than this pass — see the PR description.
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

// Unlike the bucketed segment values above, this total comes from the same currency-safe rollup
// every other total in the app uses — it is correct even when the ring's own proportions are not.
const summary = ref<StockValueSummary | null>(null);

onMounted(async () => {
  try {
    summary.value = await fetchStocksTotalValue();
  } catch (err) {
    console.error('Failed to fetch stock value:', err);
  }
});

const formattedTotal = computed(() => {
  const s = summary.value;
  if (!s) return '';
  if (s.totalValue === null) return '—';
  return formatMoney(s.totalValue, s.currency);
});

const { missingRateMessage } = useRateDisclosure(computed(() => summary.value));
</script>
