<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-3">{{ t('property.rentVsMarket.title') }}</h2>

    <div v-if="metrics.marketMonthlyRent === null" class="text-sm text-text-muted">
      <p class="mb-2">{{ t('property.rentVsMarket.noEstimate') }}</p>
      <form class="flex gap-2" @submit.prevent="submitEstimate">
        <BaseInput
          v-model.number="estimate"
          type="number"
          min="1"
          :placeholder="t('property.rentVsMarket.estimatePlaceholder')"
          class="flex-1 min-w-0"
          required
        />
        <BaseButton type="submit" size="sm">{{ t('property.rentVsMarket.save') }}</BaseButton>
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
        {{
          t('property.rentVsMarket.belowExplainer', {
            rent: money(metrics.contractedMonthlyRent),
            market: money(metrics.marketMonthlyRent),
            uplift: money(metrics.annualRentUplift),
          })
        }}
      </p>
      <p v-else class="text-sm text-text mt-2">
        {{
          t('property.rentVsMarket.atOrAboveExplainer', {
            rent: money(metrics.contractedMonthlyRent),
            market: money(metrics.marketMonthlyRent),
          })
        }}
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
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

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
  if (gap > 0) return t('property.rentVsMarket.belowMarket', { gap: formatPercent(gap) });
  if (gap < 0)
    return t('property.rentVsMarket.aboveMarket', { gap: formatPercent(Math.abs(gap)) });
  return t('property.rentVsMarket.atMarket');
});

function submitEstimate() {
  if (estimate.value && estimate.value > 0) {
    emit('add-estimate', estimate.value);
    estimate.value = null;
  }
}
</script>
