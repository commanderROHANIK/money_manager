<template>
  <div>
    <h2 class="text-xl font-semibold mb-4">All Properties</h2>

    <p v-if="properties.length === 0" class="text-sm text-gray-500">
      No properties yet.
    </p>

    <ul v-else class="space-y-2 max-h-[360px] overflow-y-auto">
      <li
        v-for="property in properties"
        :key="property.id"
        class="p-3 rounded-lg shadow bg-white dark:bg-gray-800"
      >
        <div class="flex justify-between items-center gap-3">
          <div class="min-w-0">
            <router-link
              :to="`/properties/${property.id}`"
              class="font-medium text-blue-600 hover:underline"
            >
              {{ property.propertyName }}
            </router-link>
            <div class="text-sm text-gray-500 dark:text-gray-400 truncate">
              {{ property.address }}
            </div>
            <div v-if="property.isRented" class="text-sm text-gray-500">
              {{ formatMoney(property.rentAmount, property.currencyCode) }} / month
              <span v-if="property.tenantName">· {{ property.tenantName }}</span>
            </div>
          </div>

          <div class="flex items-center gap-3 whitespace-nowrap">
            <span
              class="text-sm font-medium px-2 py-1 rounded-full"
              :class="{
                'bg-green-100 text-green-800 dark:bg-green-800 dark:text-green-100': property.isRented,
                'bg-yellow-100 text-yellow-800 dark:bg-yellow-800 dark:text-yellow-100': !property.isRented,
              }"
            >
              {{ property.isRented ? 'Rented' : 'Vacant' }}
            </span>
            <button
              class="text-gray-400 hover:text-red-600 text-sm"
              :aria-label="`Delete ${property.propertyName}`"
              @click="confirmDelete(property)"
            >
              Delete
            </button>
          </div>
        </div>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { RentalProperty } from '../../../models/models';
import { formatMoney } from '../../../utils/money';

defineProps<{
  properties: RentalProperty[];
}>();

// The parent has always listened for this; the button that raises it was never built.
const emit = defineEmits<{ (e: 'delete-property', id: number): void }>();

function confirmDelete(property: RentalProperty) {
  // Deleting a property takes its tenancies, ledger and history with it, so this is worth
  // a confirmation step.
  const message =
    `Delete "${property.propertyName}"? This also removes its tenancies, transactions, ` +
    'valuations and timeline.';

  if (window.confirm(message)) {
    emit('delete-property', property.id);
  }
}
</script>
