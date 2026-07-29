<template>
  <div>
    <h2 class="text-lg font-semibold mb-1">Add a loan</h2>
    <p class="text-xs text-gray-500 mb-4">
      Track what you owe — the amounts below are what interest and payoff progress are measured
      against.
    </p>

    <form @submit.prevent="submit" class="space-y-3">
      <input v-model="form.loanName" placeholder="Loan Name" class="p-2 border rounded w-full" required />

      <div>
        <input
          v-model.number="form.loanAmount"
          placeholder="Original amount"
          class="p-2 border rounded w-full"
          type="number"
          min="0"
          required
        />
        <p class="text-xs text-gray-500 mt-1">Original amount borrowed, before any repayments.</p>
      </div>

      <div>
        <input
          v-model.number="form.remainingBalance"
          placeholder="Remaining balance"
          class="p-2 border rounded w-full"
          type="number"
          min="0"
          required
        />
        <p class="text-xs text-gray-500 mt-1">
          What's still owed today — this is what interest and payoff progress are calculated from.
        </p>
      </div>

      <div>
        <input
          v-model.number="form.interestRate"
          placeholder="Interest rate (%)"
          class="p-2 border rounded w-full"
          type="number"
          min="0"
          step="0.01"
          required
        />
        <p class="text-xs text-gray-500 mt-1">Annual rate, e.g. 4.5 for 4.5%.</p>
      </div>

      <div>
        <input v-model="form.dueDate" type="date" class="p-2 border rounded w-full" required />
        <p class="text-xs text-gray-500 mt-1">Date the loan is scheduled to be fully repaid.</p>
      </div>

      <div>
        <select v-model="form.currencyCode" class="p-2 border rounded w-full">
          <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
        </select>
        <p class="text-xs text-gray-500 mt-1">Currency this loan is denominated in.</p>
      </div>

      <div>
        <label class="flex items-center gap-1">
          <input v-model="form.isPaidOff" type="checkbox" />
          <span>Paid off</span>
        </label>
        <p class="text-xs text-gray-500 mt-1">Check if this loan has already been fully repaid.</p>
      </div>

      <button type="submit" class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded">
        Add loan
      </button>
    </form>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import type { Loan } from '../../../models/models';
import { CURRENCIES } from '../../../utils/currencies';

const emit = defineEmits<{ (e: 'create', payload: Omit<Loan, 'id'>): void }>();

function emptyForm() {
  return {
    loanName: '',
    loanAmount: 0,
    remainingBalance: 0,
    interestRate: 0,
    dueDate: '',
    isPaidOff: false,
    currencyCode: 'EUR',
  };
}

const form = reactive(emptyForm());

function submit() {
  emit('create', { ...form });
  Object.assign(form, emptyForm());
}
</script>
