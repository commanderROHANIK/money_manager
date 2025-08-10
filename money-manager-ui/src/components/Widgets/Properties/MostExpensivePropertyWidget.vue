<template>
  <div class="p-4 rounded-lg shadow bg-white dark:bg-gray-800">
    <h2 class="text-xl font-semibold mb-4 text-white">Most Expensive Property</h2>

    <div v-if="mostExpensive" class="space-y-2">
      <div class="text-lg font-medium">{{ mostExpensive.propertyName }}</div>
      <div class="text-sm text-gray-500 dark:text-gray-400">
        {{ mostExpensive.address }}
      </div>
      <div class="text-sm text-gray-600 dark:text-gray-300">
        Monthly Rent: 
        <span class="font-semibold text-green-700 dark:text-green-300">
          {{ formatCurrency(mostExpensive.rentAmount) }}
        </span>
      </div>
      <span
        class="inline-block text-xs font-medium px-2 py-1 rounded-full"
        :class="{
          'bg-green-100 text-green-800 dark:bg-green-800 dark:text-green-100': mostExpensive.isRented,
          'bg-yellow-100 text-yellow-800 dark:bg-yellow-800 dark:text-yellow-100': !mostExpensive.isRented,
        }"
      >
        {{ mostExpensive.isRented ? 'Rented' : 'Vacant' }}
      </span>
    </div>

    <div v-else class="text-sm text-gray-500 dark:text-gray-400">
      No properties available.
    </div>
  </div>
</template>

<script setup lang="ts">
import type { RentalProperty } from '../../../models/models';
import { computed, defineProps } from 'vue';

const props = defineProps<{
  properties: RentalProperty[];
}>();

const mostExpensive = computed(() =>
  props.properties.reduce((max, p) =>
    !max || p.rentAmount > max.rentAmount ? p : max, null as RentalProperty | null
  )
);

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(amount);
}
</script>
