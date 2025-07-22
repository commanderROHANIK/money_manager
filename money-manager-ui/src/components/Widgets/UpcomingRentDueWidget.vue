<template>
  <div>
    <h2 class="text-xl font-semibold mb-4">Upcoming Rent Due</h2>
    <ul v-if="upcomingRents.length > 0" class="space-y-2">
      <li
        v-for="property in upcomingRents"
        :key="property.id"
        class="p-3 rounded-lg shadow bg-white dark:bg-gray-800 flex justify-between items-center"
      >
        <div>
          <div class="font-medium">{{ property.propertyName }}</div>
          <div class="text-sm text-gray-500 dark:text-gray-400">{{ formatDate(property.rentDueDate) }}</div>
        </div>
        <div class="text-green-600 font-semibold">{{ formatCurrency(property.rentAmount) }}</div>
      </li>
    </ul>
    <p v-else class="text-gray-500 dark:text-gray-400">No rent due in the next 30 days.</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { RentalProperty } from '../../models/models';

const props = defineProps<{
  properties: RentalProperty[];
}>();

function formatDate(iso: string): string {
  const date = new Date(iso);
  return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' }).format(amount);
}

const today = new Date();
const inThirtyDays = new Date();
inThirtyDays.setDate(today.getDate() + 30);

const upcomingRents = computed(() =>
  props.properties
    .filter(p => {
      const due = new Date(p.rentDueDate);
      return due >= today && due <= inThirtyDays;
    })
    .sort((a, b) => new Date(a.rentDueDate).getTime() - new Date(b.rentDueDate).getTime())
);
</script>

<style scoped>
ul {
  max-height: 300px;
  overflow-y: auto;
}
</style>
