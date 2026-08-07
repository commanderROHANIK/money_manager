<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-1">Add a property</h2>
    <p class="text-xs text-text-muted mb-4">
      The purchase price and date are what every return figure is measured against — worth
      entering even if they are approximate.
    </p>

    <form class="grid grid-cols-1 md:grid-cols-3 gap-3" @submit.prevent="submit">
      <BaseInput v-model="form.propertyName" placeholder="Name" class="md:col-span-2" required />
      <BaseSelect v-model.number="form.propertyType">
        <option v-for="(label, value) in PROPERTY_TYPE_LABELS" :key="value" :value="Number(value)">
          {{ label }}
        </option>
      </BaseSelect>

      <BaseInput v-model="form.address" placeholder="Address" class="md:col-span-2" required />
      <BaseInput v-model="form.city" placeholder="City" />

      <BaseInput v-model.number="form.purchasePrice" type="number" min="0" placeholder="Purchase price" />
      <BaseInput v-model="form.purchaseDate" type="date" />
      <BaseSelect v-model="form.currencyCode">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <BaseInput v-model.number="form.sizeSqm" type="number" min="0" placeholder="Size (m²)" />
      <BaseInput v-model.number="form.bedrooms" type="number" min="0" placeholder="Bedrooms" />

      <BaseButton type="submit" class="md:col-span-3">Add property</BaseButton>
    </form>

    <p v-if="error" class="mt-3 text-sm text-danger">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue';
import { PROPERTY_TYPE_LABELS } from '../../../utils/labels';
import { CURRENCIES } from '../../../utils/currencies';
import type { RentalPropertyRequest } from '../../../services/propertyApi';
import BaseInput from '../../ui/BaseInput.vue';
import BaseSelect from '../../ui/BaseSelect.vue';
import BaseButton from '../../ui/BaseButton.vue';

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
