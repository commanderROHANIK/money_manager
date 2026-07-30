<template>
  <div>
    <div class="flex items-baseline justify-between mb-4">
      <h2 class="font-heading text-lg font-bold">Investment performance</h2>
      <span class="text-xs text-text-muted">as of {{ formatDate(metrics.asOf) }}</span>
    </div>

    <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
      <div v-for="tile in tiles" :key="tile.label" class="p-3 rounded-lg bg-surface-2">
        <p class="text-xs text-text-muted">{{ tile.label }}</p>
        <p class="text-lg font-bold tabular-nums" :class="tile.tone">{{ tile.value }}</p>
        <p v-if="tile.hint" class="text-[11px] text-text-muted mt-0.5">{{ tile.hint }}</p>
      </div>
    </div>

    <!-- Warnings are part of the answer, not an error state: they say which inputs are
         missing so a soft number is not read as a hard one. -->
    <ul v-if="metrics.warnings.length" class="mt-4 space-y-1">
      <li
        v-for="warning in metrics.warnings"
        :key="warning.code"
        class="text-xs text-accent bg-accent-soft border border-border rounded px-2 py-1"
      >
        {{ warning.message }}
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { PropertyMetrics } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatDate, formatPercent } from '../../../utils/labels';

const props = defineProps<{ metrics: PropertyMetrics }>();

function money(value: number | null): string {
  return value === null ? '—' : formatMoney(value, props.metrics.currencyCode);
}

function sign(value: number | null): string {
  if (value === null) return '';
  return value >= 0 ? 'text-primary' : 'text-danger';
}

const tiles = computed(() => [
  {
    label: 'Total ROI',
    value: formatPercent(props.metrics.totalRoi),
    tone: sign(props.metrics.totalRoi),
    hint: props.metrics.annualizedRoi === null
      ? undefined
      : `${formatPercent(props.metrics.annualizedRoi)} a year`,
  },
  {
    label: 'Monthly cash flow',
    value: money(props.metrics.monthlyCashFlow),
    tone: sign(props.metrics.monthlyCashFlow),
    hint: 'after running costs and mortgage',
  },
  {
    label: 'Cash invested',
    value: money(props.metrics.cashInvested),
    tone: '',
    hint: props.metrics.totalInvested === null
      ? undefined
      : `${money(props.metrics.totalInvested)} total cost`,
  },
  {
    label: 'Equity',
    value: money(props.metrics.equity),
    tone: '',
    hint: props.metrics.currentValue === null
      ? undefined
      : `${money(props.metrics.currentValue)} value`,
  },
  {
    label: 'Gross yield',
    value: formatPercent(props.metrics.grossYield, 2),
    tone: '',
    hint: 'rent ÷ total invested',
  },
  {
    label: 'Cap rate',
    value: formatPercent(props.metrics.capRate, 2),
    tone: '',
    hint: 'net income ÷ value',
  },
  {
    label: 'Cash-on-cash',
    value: formatPercent(props.metrics.cashOnCashReturn, 2),
    tone: sign(props.metrics.cashOnCashReturn),
    hint: 'return on money actually put in',
  },
  {
    label: 'Occupancy',
    value: formatPercent(props.metrics.occupancyRate, 0),
    tone: '',
    hint: props.metrics.yearsHeld === null
      ? undefined
      : `held ${props.metrics.yearsHeld.toFixed(1)} yrs`,
  },
]);
</script>
