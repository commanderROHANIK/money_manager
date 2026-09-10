<template>
  <div>
    <div class="font-heading text-2xl font-bold text-primary-strong tabular-nums">
      {{ formattedDividend }}
    </div>
    <p class="text-xs text-text-muted mt-1">
      Estimate only — assumes a {{ (ASSUMED_YIELD * 100).toFixed(0) }}% yield.
      Actual dividends are not tracked yet.
    </p>
  </div>
</template>

<script lang="ts" setup>
import { ref, computed, onMounted } from 'vue';
import { fetchStocksTotalValue } from '../../../services/api';
import { formatMoney } from '../../../utils/money';
import type { StockValueSummary } from '../../../models/models';

/**
 * There is no dividend data in the schema, so this is a flat assumption rather than a
 * measurement. It is surfaced in the UI rather than hidden, so the figure is not mistaken
 * for real income.
 */
const ASSUMED_YIELD = 0.02;

const summary = ref<StockValueSummary | null>(null);

onMounted(async () => {
  try {
    summary.value = await fetchStocksTotalValue();
  } catch (error) {
    console.error('Failed to load stock value:', error);
  }
});

// The portfolio's own converted total (CurrencyRollup.Sum on the backend) rather than a
// client-side sum across whatever currencies the holdings happen to be in — this used to add raw
// amounts across currencies as if they were the same unit. Blank when a rate is missing, same as
// the total it is built from: an estimate multiplied by an unconvertible figure is still
// unconvertible.
const formattedDividend = computed(() => {
  const total = summary.value?.totalValue;
  if (total === null || total === undefined) return '—';
  return formatMoney(total * ASSUMED_YIELD, summary.value?.currency ?? 'EUR');
});
</script>
