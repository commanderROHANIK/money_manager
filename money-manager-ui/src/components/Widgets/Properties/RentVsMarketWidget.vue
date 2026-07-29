<template>
  <div>
    <div class="flex items-baseline justify-between gap-2 mb-3">
      <h2 class="text-xl font-semibold">Rent vs market</h2>
      <button
        class="text-xs text-blue-600 hover:underline disabled:opacity-50"
        :disabled="refreshing"
        @click="emit('refresh')"
      >
        {{ refreshing ? 'Checking…' : 'Check market' }}
      </button>
    </div>

    <div v-if="metrics.marketMonthlyRent === null" class="text-sm text-gray-500">
      <p class="mb-2">
        No market estimate yet. "Check market" compares this property against similar
        lettings; if there are too few nearby, enter your own estimate below.
      </p>
      <form @submit.prevent="submitEstimate" class="flex gap-2">
        <input
          v-model.number="estimate"
          type="number"
          min="1"
          placeholder="Market rent / month"
          class="p-2 border rounded flex-1 min-w-0"
          required
        />
        <button type="submit" class="bg-blue-600 hover:bg-blue-700 text-white px-3 rounded text-sm">
          Save
        </button>
      </form>
    </div>

    <div v-else>
      <p class="text-3xl font-bold" :class="isBelowMarket ? 'text-amber-600' : 'text-green-600'">
        {{ headline }}
      </p>

      <p v-if="isBelowMarket" class="text-sm text-gray-700 dark:text-gray-300 mt-2">
        You charge {{ money(metrics.contractedMonthlyRent) }} against an estimated
        {{ money(metrics.marketMonthlyRent) }}. Closing the gap is worth
        <strong>{{ money(metrics.annualRentUplift) }}</strong> a year.
      </p>
      <p v-else class="text-sm text-gray-700 dark:text-gray-300 mt-2">
        You charge {{ money(metrics.contractedMonthlyRent) }} against an estimated
        {{ money(metrics.marketMonthlyRent) }} — at or above market.
      </p>

      <!-- Provenance is never optional: a market figure without its source and date is
           an authoritative-looking guess. -->
      <p v-if="marketPoint" class="text-[11px] text-gray-500 mt-3">
        {{ sourceLabel }} · as at {{ formatDate(marketPoint.effectiveFrom) }}
        <span v-if="marketPoint.notes"><br />{{ marketPoint.notes }}</span>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import type { PropertyMetrics, RentPricePoint } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatDate, formatPercent } from '../../../utils/labels';

const props = defineProps<{
  metrics: PropertyMetrics;
  marketPoint?: RentPricePoint | null;
  refreshing?: boolean;
}>();

const emit = defineEmits<{
  (e: 'add-estimate', amount: number): void;
  (e: 'refresh'): void;
}>();

const estimate = ref<number | null>(null);

function money(value: number | null): string {
  return value === null ? '—' : formatMoney(value, props.metrics.currencyCode);
}

const isBelowMarket = computed(() => (props.metrics.rentGapPercent ?? 0) > 0);

const headline = computed(() => {
  const gap = props.metrics.rentGapPercent;
  if (gap === null) return '—';
  if (gap > 0) return `${formatPercent(gap)} below market`;
  if (gap < 0) return `${formatPercent(Math.abs(gap))} above market`;
  return 'At market';
});

const sourceLabel = computed(() =>
  props.marketPoint?.providerKey === 'peer-comparables'
    ? 'Comparable lettings'
    : 'Your own estimate'
);

function submitEstimate() {
  if (estimate.value && estimate.value > 0) {
    emit('add-estimate', estimate.value);
    estimate.value = null;
  }
}
</script>
