<template>
  <div>
    <div class="flex items-baseline justify-between mb-4">
      <h2 class="font-heading text-lg font-bold">{{ t('property.metrics.title') }}</h2>
      <span class="text-xs text-text-muted">{{
        t('property.metrics.asOf', { date: formatDate(metrics.asOf) })
      }}</span>
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
        class="text-xs text-accent-strong bg-accent-soft border border-border rounded px-2 py-1"
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
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps<{ metrics: PropertyMetrics }>();

function money(value: number | null): string {
  return value === null ? '—' : formatMoney(value, props.metrics.currencyCode);
}

function sign(value: number | null): string {
  if (value === null) return '';
  return value >= 0 ? 'text-primary-strong' : 'text-danger';
}

const tiles = computed(() => [
  {
    label: t('property.metrics.roi'),
    value: formatPercent(props.metrics.totalRoi),
    tone: sign(props.metrics.totalRoi),
    hint: props.metrics.annualizedRoi === null
      ? undefined
      : t('property.metrics.roiHint', { rate: formatPercent(props.metrics.annualizedRoi) }),
  },
  {
    label: t('property.metrics.cashFlow'),
    value: money(props.metrics.monthlyCashFlow),
    tone: sign(props.metrics.monthlyCashFlow),
    hint: t('property.metrics.cashFlowHint'),
  },
  {
    label: t('property.metrics.invested'),
    value: money(props.metrics.cashInvested),
    tone: '',
    hint: props.metrics.totalInvested === null
      ? undefined
      : t('property.metrics.investedHint', { amount: money(props.metrics.totalInvested) }),
  },
  {
    label: t('property.metrics.equity'),
    value: money(props.metrics.equity),
    tone: '',
    hint: props.metrics.currentValue === null
      ? undefined
      : t('property.metrics.equityHint', { amount: money(props.metrics.currentValue) }),
  },
  {
    label: t('property.metrics.grossYield'),
    value: formatPercent(props.metrics.grossYield, 2),
    tone: '',
    hint: t('property.metrics.grossYieldHint'),
  },
  {
    label: t('property.metrics.capRate'),
    value: formatPercent(props.metrics.capRate, 2),
    tone: '',
    hint: t('property.metrics.capRateHint'),
  },
  {
    label: t('property.metrics.cashOnCash'),
    value: formatPercent(props.metrics.cashOnCashReturn, 2),
    tone: sign(props.metrics.cashOnCashReturn),
    hint: t('property.metrics.cashOnCashHint'),
  },
  {
    label: t('property.metrics.occupancy'),
    value: formatPercent(props.metrics.occupancyRate, 0),
    tone: '',
    hint: props.metrics.yearsHeld === null
      ? undefined
      : t('property.metrics.occupancyHint', { years: props.metrics.yearsHeld.toFixed(1) }),
  },
]);
</script>
