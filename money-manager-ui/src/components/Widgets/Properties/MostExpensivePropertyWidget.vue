<template>
  <div>
    <!-- The heading was text-white on a white card, so it was invisible in light mode. -->
    <h2 class="text-lg font-semibold mb-4">Highest Rent</h2>

    <div v-if="mostExpensive" class="space-y-2">
      <router-link
        :to="`/properties/${mostExpensive.id}`"
        class="text-lg font-medium text-blue-600 hover:underline"
      >
        {{ mostExpensive.propertyName }}
      </router-link>
      <div class="text-sm text-gray-500">
        {{ mostExpensive.address }}
      </div>
      <div class="text-sm text-gray-600">
        Monthly Rent:
        <span class="font-semibold text-green-700">
          {{ formatMoney(mostExpensive.rentAmount, mostExpensive.currencyCode) }}
        </span>
      </div>
      <span
        class="inline-block text-xs font-medium px-2 py-1 rounded-full"
        :class="{
          'bg-green-100 text-green-800': mostExpensive.isRented,
          'bg-yellow-100 text-yellow-800': !mostExpensive.isRented,
        }"
      >
        {{ mostExpensive.isRented ? 'Rented' : 'Vacant' }}
      </span>
    </div>

    <div v-else class="text-sm text-gray-500">
      No properties available.
    </div>
  </div>
</template>

<script setup lang="ts">
import type { RentalProperty } from '../../../models/models';
import { computed } from 'vue';
import { formatMoney } from '../../../utils/money';

const props = defineProps<{
  properties: RentalProperty[];
}>();

const mostExpensive = computed(() =>
  props.properties.reduce((max, p) =>
    !max || p.rentAmount > max.rentAmount ? p : max, null as RentalProperty | null
  )
);
</script>
