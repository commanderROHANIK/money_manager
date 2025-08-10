<template>
  <div class="bg-white p-6 rounded-2xl shadow-md col-span-1">
    <h2 class="text-lg font-semibold mb-2">Add New Loan</h2>
    <form @submit.prevent="handleSubmit" class="space-y-2">
      <input v-model="loan.loanName" placeholder="Loan Name" class="p-2 border rounded w-full" required />
      <input v-model.number="loan.loanAmount" placeholder="Amount" class="p-2 border rounded w-full" type="number" required />
      <input v-model.number="loan.remainingBalance" placeholder="Remaining" class="p-2 border rounded w-full" type="number" required />
      <input v-model.number="loan.interestRate" placeholder="Interest Rate (%)" class="p-2 border rounded w-full" type="number" required />
      <input v-model="loan.dueDate" type="date" class="p-2 border rounded w-full" required />
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

const emit = defineEmits(['add-loan']);

const loan = ref<Loan>({
  id: 0,
  loanName: '',
  loanAmount: 0,
  remainingBalance: 0,
  interestRate: 0,
  dueDate: '',
  isPaidOff: false,
});

function handleSubmit() {
  emit('add-loan', { ...loan.value });
  loan.value = {
    id: 0,
    loanName: '',
    loanAmount: 0,
    remainingBalance: 0,
    interestRate: 0,
    dueDate: '',
    isPaidOff: false,
  };
}
</script>
