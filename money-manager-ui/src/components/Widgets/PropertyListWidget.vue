<template>
  <div>
    <h2 class="text-xl font-semibold mb-4">All Properties</h2>
    <ul class="space-y-2 max-h-[300px] overflow-y-auto">
      <li
        v-for="property in properties"
        :key="property.id"
        class="p-3 rounded-lg shadow bg-white dark:bg-gray-800"
      >
        <div class="flex justify-between items-center">
          <div>
            <div class="font-medium">{{ property.propertyName }}</div>
            <div class="text-sm text-gray-500 dark:text-gray-400">{{ property.address }}</div>
          </div>
          <span
            class="text-sm font-medium px-2 py-1 rounded-full"
            :class="{
              'bg-green-100 text-green-800 dark:bg-green-800 dark:text-green-100': property.isRented,
              'bg-yellow-100 text-yellow-800 dark:bg-yellow-800 dark:text-yellow-100': !property.isRented,
            }"
          >
            {{ property.isRented ? 'Rented' : 'Vacant' }}
          </span>
        </div>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { RentalProperty } from '../../models/models';

defineProps<{
  properties: RentalProperty[];
}>();
</script>
