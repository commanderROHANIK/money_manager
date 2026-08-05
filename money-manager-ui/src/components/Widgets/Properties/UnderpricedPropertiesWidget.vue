<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-1">Money left on the table</h2>
    <p class="text-xs text-text-muted mb-3">Properties let below their estimated market rent.</p>

    <p v-if="underpriced.length === 0" class="text-sm text-text-muted">
      Nothing below market — or no market estimates recorded yet. Add one on a property's page
      to see the comparison.
    </p>

    <div v-else>
      <p class="font-heading text-3xl font-extrabold tabular-nums text-accent-strong mb-3">
        {{ totalUpliftLabel }}<span class="text-base font-normal text-text-muted"> / year</span>
      </p>

      <ul>
        <ListRow v-for="item in underpriced" :key="item.propertyId">
          <template #title>
            <router-link
              :to="`/properties/${item.propertyId}`"
              class="text-primary-strong hover:underline truncate"
            >
              {{ item.propertyName }}
            </router-link>
          </template>
          <template #trailing>
            <span class="text-sm whitespace-nowrap">
              <span class="text-accent-strong font-medium">{{ formatPercent(item.rentGapPercent) }}</span>
              <span class="text-text-muted">
                · {{ formatMoney(item.annualRentUplift, item.currencyCode) }}/yr
              </span>
            </span>
          </template>
        </ListRow>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { PropertyMetrics } from '../../../models/models';
import { formatMoney, sumSameCurrency } from '../../../utils/money';
import { formatPercent } from '../../../utils/labels';
import ListRow from '../../ui/ListRow.vue';

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
