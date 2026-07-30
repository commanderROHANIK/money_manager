<template>
  <StatCard label="Total Monthly Rent" :value="formattedTotal">
    <template v-if="mixed" #value>
      {{ formattedTotal }}
      <span class="block text-xs font-normal text-text-muted mt-1">
        Properties span multiple currencies — showing the unconverted sum.
      </span>
    </template>
  </StatCard>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { RentalProperty } from '../../../models/models';
import { formatMoney, sumSameCurrency } from '../../../utils/money';
import StatCard from '../../ui/StatCard.vue';

const props = defineProps<{
  properties: RentalProperty[];
}>();

// This read a non-existent `monthlyRent` field, so the widget showed 0 for every
// portfolio and the type error broke the production build.
const summed = computed(() =>
  sumSameCurrency(
    props.properties,
    (p) => p.rentAmount,
    (p) => p.currencyCode
  )
);

const mixed = computed(() => summed.value.mixed);

const formattedTotal = computed(() =>
  formatMoney(summed.value.total, summed.value.currency)
);
</script>
