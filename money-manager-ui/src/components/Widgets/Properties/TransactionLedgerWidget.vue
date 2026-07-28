<template>
  <div>
    <h2 class="text-xl font-semibold mb-4">Ledger</h2>

    <form @submit.prevent="submit" class="flex flex-wrap gap-2 mb-4">
      <input v-model="form.date" type="date" class="p-2 border rounded" required />
      <select v-model.number="form.category" class="p-2 border rounded">
        <optgroup v-for="group in TRANSACTION_CATEGORY_GROUPS" :key="group.label" :label="group.label">
          <option v-for="category in group.categories" :key="category" :value="category">
            {{ TRANSACTION_CATEGORY_LABELS[category] }}
          </option>
        </optgroup>
      </select>
      <input
        v-model.number="form.amount"
        type="number"
        step="0.01"
        min="0.01"
        placeholder="Amount"
        class="p-2 border rounded w-32"
        required
      />
      <input v-model="form.description" placeholder="Note" class="p-2 border rounded flex-1 min-w-[120px]" />
      <button type="submit" class="bg-green-600 hover:bg-green-700 text-white px-4 rounded">
        Record
      </button>
    </form>

    <p class="text-xs text-gray-500 mb-3">
      Enter every amount as a positive number — the category decides whether it is money in
      or out.
    </p>

    <p v-if="transactions.length === 0" class="text-sm text-gray-500">
      No entries yet. Recording rent received and costs paid is what makes the return figures
      real rather than projected.
    </p>

    <ul v-else class="divide-y max-h-[320px] overflow-y-auto">
      <li v-for="entry in transactions" :key="entry.id" class="py-2 flex justify-between gap-3">
        <div class="min-w-0">
          <p class="text-sm font-medium truncate">
            {{ TRANSACTION_CATEGORY_LABELS[entry.category] }}
          </p>
          <p class="text-xs text-gray-500 truncate">
            {{ formatDate(entry.date) }}<span v-if="entry.description"> · {{ entry.description }}</span>
          </p>
        </div>
        <div class="flex items-center gap-3 whitespace-nowrap">
          <span :class="isIncome(entry.category) ? 'text-green-600' : 'text-red-600'">
            {{ isIncome(entry.category) ? '+' : '−' }}{{ formatMoney(entry.amount, entry.currencyCode) }}
          </span>
          <button
            class="text-gray-400 hover:text-red-600 text-sm"
            :aria-label="`Delete ${TRANSACTION_CATEGORY_LABELS[entry.category]}`"
            @click="emit('delete', entry.id)"
          >
            ✕
          </button>
        </div>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import { TransactionCategory, type PropertyTransaction } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import {
  TRANSACTION_CATEGORY_GROUPS,
  TRANSACTION_CATEGORY_LABELS,
  formatDate,
  isIncome,
} from '../../../utils/labels';

defineProps<{ transactions: PropertyTransaction[] }>();

const emit = defineEmits<{
  (e: 'create', payload: { date: string; amount: number; category: number; description: string }): void;
  (e: 'delete', id: number): void;
}>();

const form = reactive({
  date: new Date().toISOString().split('T')[0],
  amount: null as number | null,
  category: TransactionCategory.RentIncome as number,
  description: '',
});

function submit() {
  if (!form.amount || form.amount <= 0) return;

  emit('create', {
    date: new Date(form.date).toISOString(),
    amount: form.amount,
    category: form.category,
    description: form.description,
  });

  form.amount = null;
  form.description = '';
}
</script>
