<template>
  <div>
    <h2 class="text-lg font-semibold mb-3">Portfolio</h2>

    <p v-if="!portfolio || portfolio.propertyCount === 0" class="text-sm text-gray-500">
      No properties yet. Add one below to start tracking what it returns.
    </p>

    <template v-else>
      <p v-if="portfolio.mixedCurrency" class="text-sm text-amber-700 mb-3">
        Your properties span several currencies, so they are not totalled here — exchange
        rates are not applied yet. Each property's own figures are still exact.
      </p>

      <div v-else class="grid grid-cols-2 md:grid-cols-5 gap-3">
        <div v-for="tile in tiles" :key="tile.label" class="p-3 rounded-lg bg-gray-50">
          <p class="text-xs text-gray-500">{{ tile.label }}</p>
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
import { formatPercent } from '../../../utils/labels';

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
