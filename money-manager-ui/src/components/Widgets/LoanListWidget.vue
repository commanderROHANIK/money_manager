<template>
  <form @submit.prevent="addLoan" class="bg-white p-4 rounded-2xl shadow-md col-span-1 md:col-span-3">
  <div class="flex flex-wrap gap-2">
    <input v-model="newLoan.loanName" placeholder="Loan Name" class="p-2 border rounded" required />
    <input v-model.number="newLoan.loanAmount" placeholder="Amount" class="p-2 border rounded" type="number" required />
    <input v-model.number="newLoan.remainingBalance" placeholder="Remaining" class="p-2 border rounded" type="number" required />
    <input v-model.number="newLoan.interestRate" placeholder="Interest Rate (%)" class="p-2 border rounded" type="number" required />
    <input v-model="newLoan.dueDate" type="date" class="p-2 border rounded" required />
    <label class="flex items-center">
      <input v-model="newLoan.isPaidOff" type="checkbox" class="mr-1" />
      Paid Off
    </label>
    <button type="submit" class="bg-green-500 text-white px-4 py-2 rounded">Add Loan</button>
  </div>
</form>

</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { fetchLoans, createLoan, deleteLoan } from '../../services/api';
import type { Loan } from '../../models/models';

const loans = ref<Loan[]>([]);

const newLoan = ref<Loan>({
  id: 0,
  loanName: '',
  loanAmount: 0,
  remainingBalance: 0,
  interestRate: 0,
  dueDate: '',
  isPaidOff: false,
});

onMounted(async () => {
  loans.value = await fetchLoans();
});

async function addLoan() {
  const created = await createLoan(newLoan.value);
  loans.value.push(created);
  newLoan.value = {
    id: 0,
    loanName: '',
    loanAmount: 0,
    remainingBalance: 0,
    interestRate: 0,
    dueDate: '',
    isPaidOff: false,
  };
}

async function removeLoan(id: number) {
  await deleteLoan(id);
  loans.value = loans.value.filter(l => l.id !== id);
}

</script>
