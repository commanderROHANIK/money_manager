<template>
  <div>
    <div class="text-2xl font-bold text-green-600">
      {{ formattedDividend }}
    </div>
    <p class="text-xs text-gray-500 mt-1">
      Estimate only — assumes a {{ (ASSUMED_YIELD * 100).toFixed(0) }}% yield.
      Actual dividends are not tracked yet.
    </p>
  </div>
</template>

<script lang="ts" setup>
import { ref, computed, onMounted } from 'vue';
import { fetchStocks } from '../../../services/api';
import { formatMoney, sumSameCurrency } from '../../../utils/money';

/**
 * There is no dividend data in the schema, so this is a flat assumption rather than a
 * measurement. It is surfaced in the UI rather than hidden, so the figure is not mistaken
 * for real income.
 */
const ASSUMED_YIELD = 0.02;

const annualDividend = ref<number | null>(0);
const currency = ref('EUR');

onMounted(async () => {
  try {
    const stocks = await fetchStocks();
    const summed = sumSameCurrency(
      stocks,
      (s) => s.currentPrice * s.sharesOwned,
      (s) => s.currencyCode
    );
    annualDividend.value = summed.total === null ? null : summed.total * ASSUMED_YIELD;
    currency.value = summed.currency;
  } catch (error) {
    console.error('Failed to load stocks:', error);
  }
});

const formattedDividend = computed(() =>
  annualDividend.value === null ? '—' : formatMoney(annualDividend.value, currency.value)
);
</script>
