<template>
  <div>
    <h2 class="text-lg font-semibold mb-1">Add a property</h2>
    <p class="text-xs text-gray-500 mb-4">
      The purchase price and date are what every return figure is measured against — worth
      entering even if they are approximate.
    </p>

    <form @submit.prevent="submit" class="grid grid-cols-1 md:grid-cols-3 gap-3">
      <input v-model="form.propertyName" placeholder="Name" class="p-2 border rounded md:col-span-2" required />
      <select v-model.number="form.propertyType" class="p-2 border rounded">
        <option v-for="(label, value) in PROPERTY_TYPE_LABELS" :key="value" :value="Number(value)">
          {{ label }}
        </option>
      </select>

      <input v-model="form.address" placeholder="Address" class="p-2 border rounded md:col-span-2" required />
      <input v-model="form.city" placeholder="City" class="p-2 border rounded" />

      <input v-model.number="form.purchasePrice" type="number" min="0" placeholder="Purchase price" class="p-2 border rounded" />
      <input v-model="form.purchaseDate" type="date" class="p-2 border rounded" />
      <select v-model="form.currencyCode" class="p-2 border rounded">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </select>

      <input v-model.number="form.sizeSqm" type="number" min="0" placeholder="Size (m²)" class="p-2 border rounded" />
      <input v-model.number="form.bedrooms" type="number" min="0" placeholder="Bedrooms" class="p-2 border rounded" />

      <button type="submit" class="bg-green-600 hover:bg-green-700 text-white py-2 rounded">
        Add property
      </button>
    </form>

    <p v-if="error" class="mt-3 text-sm text-red-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue';
import { PROPERTY_TYPE_LABELS } from '../../../utils/labels';
import { CURRENCIES } from '../../../utils/currencies';
import type { RentalPropertyRequest } from '../../../services/propertyApi';

const emit = defineEmits<{ (e: 'create', payload: RentalPropertyRequest): void }>();

const error = ref('');

const form = reactive({
  propertyName: '',
  address: '',
  city: '',
  propertyType: 0,
  purchasePrice: null as number | null,
  purchaseDate: '',
  currencyCode: 'EUR',
  sizeSqm: null as number | null,
  bedrooms: null as number | null,
});

function submit() {
  error.value = '';

  emit('create', {
    propertyName: form.propertyName,
    address: form.address,
    city: form.city || null,
    propertyType: form.propertyType,
    purchasePrice: form.purchasePrice,
    purchaseDate: form.purchaseDate ? new Date(form.purchaseDate).toISOString() : null,
    status: 0,
    currencyCode: form.currencyCode,
    sizeSqm: form.sizeSqm,
    bedrooms: form.bedrooms,
  });

  form.propertyName = '';
  form.address = '';
  form.city = '';
  form.purchasePrice = null;
  form.purchaseDate = '';
  form.sizeSqm = null;
  form.bedrooms = null;
}
</script>
