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
          <div class="text-sm text-gray-500 dark:text-gray-400">
            {{ formatDueDate(property.rentDueDate) }}
          </div>
        </div>
        <div class="text-green-600 font-semibold">
          {{ formatMoney(property.rentAmount, property.currencyCode) }}
        </div>
      </li>
    </ul>
    <p v-else class="text-gray-500 dark:text-gray-400">No rent due in the next 30 days.</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { RentalProperty } from '../../../models/models';
import { formatMoney } from '../../../utils/money';

const props = defineProps<{
  properties: RentalProperty[];
}>();

// A vacant property has no next rent date at all, so this has to cope with null rather
// than turning it into an Invalid Date.
function formatDueDate(iso: string | null | undefined): string {
  if (!iso) return 'No tenancy';
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

const today = new Date();
const inThirtyDays = new Date();
inThirtyDays.setDate(today.getDate() + 30);

const upcomingRents = computed(() =>
  props.properties
    .filter((p) => {
      if (!p.rentDueDate) return false;
      const due = new Date(p.rentDueDate);
      return due >= today && due <= inThirtyDays;
    })
    .sort(
      (a, b) => new Date(a.rentDueDate!).getTime() - new Date(b.rentDueDate!).getTime()
    )
);
</script>

<style scoped>
ul {
  max-height: 300px;
  overflow-y: auto;
}
</style>
