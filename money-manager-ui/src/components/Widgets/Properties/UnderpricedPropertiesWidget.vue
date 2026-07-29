<template>
  <div>
    <h2 class="text-lg font-semibold mb-1">Money left on the table</h2>
    <p class="text-xs text-gray-500 mb-3">Properties let below their estimated market rent.</p>

    <p v-if="underpriced.length === 0" class="text-sm text-gray-500">
      Nothing below market — or no market estimates recorded yet. Add one on a property's page
      to see the comparison.
    </p>

    <div v-else>
      <p class="text-3xl font-bold text-amber-600 mb-3">
        {{ totalUpliftLabel }}<span class="text-base font-normal text-gray-500"> / year</span>
      </p>

      <ul class="divide-y">
        <li v-for="item in underpriced" :key="item.propertyId" class="py-2 flex justify-between gap-2">
          <router-link
            :to="`/properties/${item.propertyId}`"
            class="text-blue-600 hover:underline truncate"
          >
            {{ item.propertyName }}
          </router-link>
          <span class="text-sm whitespace-nowrap">
            <span class="text-amber-600 font-medium">{{ formatPercent(item.rentGapPercent) }}</span>
            <span class="text-gray-500">
              · {{ formatMoney(item.annualRentUplift, item.currencyCode) }}/yr
            </span>
          </span>
        </li>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { PropertyMetrics } from '../../../models/models';
import { formatMoney, sumSameCurrency } from '../../../utils/money';
import { formatPercent } from '../../../utils/labels';

const props = defineProps<{ metrics: PropertyMetrics[] }>();

const underpriced = computed(() =>
  props.metrics
    .filter((m) => (m.rentGapPercent ?? 0) > 0 && (m.annualRentUplift ?? 0) > 0)
    .sort((a, b) => (b.annualRentUplift ?? 0) - (a.annualRentUplift ?? 0))
);

const totalUpliftLabel = computed(() => {
  const summed = sumSameCurrency(
    underpriced.value,
    (m) => m.annualRentUplift ?? 0,
    (m) => m.currencyCode
  );
  const label = formatMoney(summed.total, summed.currency);
  return summed.mixed ? `${label} (mixed currencies)` : label;
});
</script>
