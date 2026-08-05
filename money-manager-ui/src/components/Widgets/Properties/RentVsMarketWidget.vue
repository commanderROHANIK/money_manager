<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-3">Rent vs market</h2>

    <div v-if="metrics.marketMonthlyRent === null" class="text-sm text-text-muted">
      <p class="mb-2">No market estimate on record for this property.</p>
      <form @submit.prevent="submitEstimate" class="flex gap-2">
        <BaseInput
          v-model.number="estimate"
          type="number"
          min="1"
          placeholder="Market rent / month"
          class="flex-1 min-w-0"
          required
        />
        <BaseButton type="submit" size="sm">Save</BaseButton>
      </form>
    </div>

    <div v-else>
      <p
        class="font-heading text-3xl font-extrabold tabular-nums"
        :class="isBelowMarket ? 'text-accent-strong' : 'text-primary-strong'"
      >
        {{ headline }}
      </p>

      <p v-if="isBelowMarket" class="text-sm text-text mt-2">
        You charge {{ money(metrics.contractedMonthlyRent) }} against an estimated
        {{ money(metrics.marketMonthlyRent) }}. Closing the gap is worth
        <strong>{{ money(metrics.annualRentUplift) }}</strong> a year.
      </p>
      <p v-else class="text-sm text-text mt-2">
        You charge {{ money(metrics.contractedMonthlyRent) }} against an estimated
        {{ money(metrics.marketMonthlyRent) }} — at or above market.
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import type { PropertyMetrics } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatPercent } from '../../../utils/labels';
import BaseInput from '../../ui/BaseInput.vue';
import BaseButton from '../../ui/BaseButton.vue';

const props = defineProps<{ metrics: PropertyMetrics }>();
const emit = defineEmits<{ (e: 'add-estimate', amount: number): void }>();

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

function submitEstimate() {
  if (estimate.value && estimate.value > 0) {
    emit('add-estimate', estimate.value);
    estimate.value = null;
  }
}
</script>
