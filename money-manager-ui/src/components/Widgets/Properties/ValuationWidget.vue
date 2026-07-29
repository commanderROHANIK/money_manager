<template>
  <div>
    <h2 class="text-lg font-semibold mb-1">Valuations</h2>
    <p class="text-xs text-gray-500 mb-3">
      Equity and appreciation are measured against the most recent figure here.
    </p>

    <form @submit.prevent="submit" class="flex flex-wrap gap-2 mb-4">
      <input v-model="form.valuedOn" type="date" class="p-2 border rounded" required />
      <input
        v-model.number="form.value"
        type="number"
        min="1"
        placeholder="Value"
        class="p-2 border rounded w-32"
        required
      />
      <button type="submit" class="bg-blue-600 hover:bg-blue-700 text-white px-4 rounded">
        Add
      </button>
    </form>

    <p v-if="valuations.length === 0" class="text-sm text-gray-500">
      None recorded — the purchase price is being used as the current value.
    </p>

    <ul v-else class="divide-y text-sm max-h-[200px] overflow-y-auto">
      <li v-for="valuation in sorted" :key="valuation.id" class="py-2 flex justify-between">
        <span class="text-gray-500">{{ formatDate(valuation.valuedOn) }}</span>
        <span class="font-medium">{{ formatMoney(valuation.value, valuation.currencyCode) }}</span>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive } from 'vue';
import type { PropertyValuation } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatDate } from '../../../utils/labels';

const props = defineProps<{ valuations: PropertyValuation[]; currencyCode: string }>();
const emit = defineEmits<{ (e: 'create', payload: { valuedOn: string; value: number }): void }>();

const sorted = computed(() =>
  [...props.valuations].sort((a, b) => b.valuedOn.localeCompare(a.valuedOn))
);

const form = reactive({
  valuedOn: new Date().toISOString().split('T')[0],
  value: null as number | null,
});

function submit() {
  if (!form.value || form.value <= 0) return;

  emit('create', {
    valuedOn: new Date(form.valuedOn).toISOString(),
    value: form.value,
  });

  form.value = null;
}
</script>
