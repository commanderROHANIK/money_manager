<template>
  <div class="bg-white p-6 rounded-2xl shadow-md col-span-1">
    <h2 class="text-lg font-semibold mb-2">Add New Loan</h2>
    <form @submit.prevent="handleSubmit" class="space-y-2">
      <input v-model="loan.loanName" placeholder="Loan Name" class="p-2 border rounded w-full" required />
      <input v-model.number="loan.loanAmount" placeholder="Amount" class="p-2 border rounded w-full" type="number" required />
      <input v-model.number="loan.remainingBalance" placeholder="Remaining" class="p-2 border rounded w-full" type="number" required />
      <input v-model.number="loan.interestRate" placeholder="Interest Rate (%)" class="p-2 border rounded w-full" type="number" required />
      <input v-model="loan.dueDate" type="date" class="p-2 border rounded w-full" required />
      <select v-model="loan.currencyCode" class="p-2 border rounded w-full">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </select>
      <label class="flex items-center">
        <input v-model="loan.isPaidOff" type="checkbox" class="mr-1" />Paid Off
      </label>
      <button type="submit" class="bg-blue-500 text-white px-4 py-1 rounded">Add Loan</button>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import type { Loan } from '../../../models/models';
import { CURRENCIES } from '../../../utils/currencies';

const emit = defineEmits(['add-loan']);

function emptyLoan(): Loan {
  return {
    id: 0,
    loanName: '',
    loanAmount: 0,
    remainingBalance: 0,
    interestRate: 0,
    dueDate: '',
    isPaidOff: false,
    currencyCode: 'EUR',
  };
}

const loan = ref<Loan>(emptyLoan());

function handleSubmit() {
  emit('add-loan', { ...loan.value });
  loan.value = emptyLoan();
}
</script>
