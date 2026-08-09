<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-1">Add a property</h2>
    <p class="text-xs text-text-muted mb-4">
      The purchase price and date are what every return figure is measured against — worth
      entering even if they are approximate.
    </p>

    <form class="grid grid-cols-1 md:grid-cols-3 gap-3" @submit.prevent="submit">
      <BaseInput
        v-model="form.propertyName"
        placeholder="Name"
        class="md:col-span-2"
        :error="errors.propertyName"
        required
      />
      <BaseSelect v-model.number="form.propertyType">
        <option v-for="(label, value) in PROPERTY_TYPE_LABELS" :key="value" :value="Number(value)">
          {{ label }}
        </option>
      </BaseSelect>

      <BaseInput
        v-model="form.address"
        placeholder="Address"
        class="md:col-span-2"
        :error="errors.address"
        required
      />
      <BaseInput v-model="form.city" placeholder="City" :error="errors.city" />

      <BaseInput
        v-model.number="form.purchasePrice"
        type="number"
        min="0"
        placeholder="Purchase price"
        :error="errors.purchasePrice"
      />
      <BaseInput v-model="form.purchaseDate" type="date" :error="errors.purchaseDate" />
      <BaseSelect v-model="form.currencyCode">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <BaseInput
        v-model.number="form.sizeSqm"
        type="number"
        min="0"
        placeholder="Size (m²)"
        :error="errors.sizeSqm"
      />
      <BaseInput
        v-model.number="form.bedrooms"
        type="number"
        min="0"
        placeholder="Bedrooms"
        :error="errors.bedrooms"
      />

      <BaseButton type="submit" class="md:col-span-3">Add property</BaseButton>
    </form>

    <p v-if="error" class="mt-3 text-sm text-danger">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import { PROPERTY_TYPE_LABELS } from '../../../utils/labels';
import { CURRENCIES } from '../../../utils/currencies';
import type { RentalPropertyRequest } from '../../../services/propertyApi';
import BaseInput from '../../ui/BaseInput.vue';
import BaseSelect from '../../ui/BaseSelect.vue';
import BaseButton from '../../ui/BaseButton.vue';

const emit = defineEmits<{ (e: 'create', payload: RentalPropertyRequest): void }>();

/**
 * Errors come in rather than being held here, because this widget does not make the request —
 * it emits, and the page calls the API. Only the page knows whether the write succeeded.
 *
 * That is also why nothing is cleared on submit. The form used to empty itself the moment it
 * emitted, which was harmless while failures were invisible and actively wrong now: the server
 * would reject the write and the messages would render against inputs the user had just watched
 * go blank. The page resets this widget by remounting it, and only after a success.
 */
withDefaults(
  defineProps<{
    /** Per-field messages, keyed as the form names its fields. */
    errors?: Record<string, string>;
    /** A failure with no single field to blame — a conflict, a network error. */
    error?: string | null;
  }>(),
  { errors: () => ({}), error: null },
);

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
}
</script>
