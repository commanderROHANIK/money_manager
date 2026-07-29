<template>
  <div>
    <div class="flex items-baseline justify-between gap-2 mb-3">
      <h2 class="text-xl font-semibold">Portfolio</h2>
      <span v-if="portfolio?.fxAsOf" class="text-xs text-gray-500">
        converted to {{ portfolio.currency }} at rates as at {{ formatDate(portfolio.fxAsOf) }}
      </span>
    </div>

    <p v-if="!portfolio || portfolio.propertyCount === 0" class="text-sm text-gray-500">
      No properties yet. Add one below to start tracking what it returns.
    </p>

    <template v-else>
      <!-- Totals are withheld rather than partially summed, so the message has to say
           exactly what is missing and what to do about it. -->
      <div
        v-if="portfolio.unconvertedCurrencies.length"
        class="text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded p-3"
      >
        <p>
          No exchange rate for
          <strong>{{ portfolio.unconvertedCurrencies.join(', ') }}</strong>
          against {{ portfolio.currency }}, so portfolio totals are not shown — adding up
          unlike currencies would give a confident wrong number.
        </p>
        <router-link to="/settings" class="text-blue-600 hover:underline">
          Add a rate →
        </router-link>
      </div>

      <div v-else class="grid grid-cols-2 md:grid-cols-5 gap-3">
        <div v-for="tile in tiles" :key="tile.label" class="p-3 rounded-lg bg-gray-50 dark:bg-gray-700">
          <p class="text-xs text-gray-500 dark:text-gray-300">{{ tile.label }}</p>
          <p class="text-lg font-bold" :class="tile.tone">{{ tile.value }}</p>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { PortfolioAnalytics } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatDate, formatPercent } from '../../../utils/labels';

const props = defineProps<{ portfolio: PortfolioAnalytics | null }>();

function money(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return formatMoney(value, props.portfolio?.currency ?? 'EUR');
}

const tiles = computed(() => {
  const p = props.portfolio;
  if (!p) return [];

  return [
    { label: 'Properties', value: String(p.propertyCount), tone: '' },
    { label: 'Cash invested', value: money(p.totalInvested), tone: '' },
    { label: 'Equity', value: money(p.totalEquity), tone: '' },
    {
      label: 'Monthly cash flow',
      value: money(p.totalMonthlyCashFlow),
      tone: (p.totalMonthlyCashFlow ?? 0) >= 0 ? 'text-green-600' : 'text-red-600',
    },
    {
      label: 'Portfolio ROI',
      value: formatPercent(p.portfolioRoi),
      tone: (p.portfolioRoi ?? 0) >= 0 ? 'text-green-600' : 'text-red-600',
    },
  ];
});
</script>
