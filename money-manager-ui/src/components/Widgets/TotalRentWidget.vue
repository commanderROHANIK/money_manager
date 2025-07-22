<template>
  <div>
    <h2 class="text-xl font-semibold mb-2">Total Monthly Rent</h2>
    <p class="text-3xl font-bold text-green-600">
      {{ formattedTotal }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { RentalProperty } from '../../models/models';

const props = defineProps<{
  properties: RentalProperty[];
}>();

const totalRent = computed(() =>
  props.properties.reduce((sum, property) => sum + (property.monthlyRent || 0), 0)
);

const formattedTotal = computed(() =>
  totalRent.value.toLocaleString(undefined, { style: 'currency', currency: 'EUR' })
);
</script>
