<template>
  <div>
    <h2 class="text-xl font-semibold mb-2">Total Monthly Rent</h2>
    <p class="text-3xl font-bold text-green-600">
      {{ formattedTotal }}
    </p>
    <p v-if="mixed" class="text-xs text-gray-500 mt-1">
      Properties span multiple currencies — showing the unconverted sum.
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { RentalProperty } from '../../../models/models';
import { formatMoney, sumSameCurrency } from '../../../utils/money';

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
