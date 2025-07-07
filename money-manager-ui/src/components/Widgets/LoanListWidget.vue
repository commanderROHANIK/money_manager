<template>
  <div class="p-6 col-span-1 md:col-span-3">
    <h2 class="text-lg font-semibold mb-4">All Loans</h2>

    <!-- Add Loan Form -->
    <form @submit.prevent="addLoan" class="flex flex-wrap gap-2 mb-6">
      <input v-model="newLoan.loanName" placeholder="Loan Name" class="p-2 border rounded flex-1 min-w-[120px]" required />
      <input v-model.number="newLoan.loanAmount" placeholder="Amount" class="p-2 border rounded w-28" type="number" required />
      <input v-model.number="newLoan.remainingBalance" placeholder="Remaining" class="p-2 border rounded w-28" type="number" required />
      <input v-model.number="newLoan.interestRate" placeholder="Interest Rate (%)" class="p-2 border rounded w-32" type="number" required />
      <input v-model="newLoan.dueDate" type="date" class="p-2 border rounded" required />
      <label class="flex items-center space-x-1">
        <input v-model="newLoan.isPaidOff" type="checkbox" />
        <span>Paid Off</span>
      </label>
      <button type="submit" class="bg-green-500 hover:bg-green-600 text-white px-4 py-2 rounded">
        Add
      </button>
    </form>

    <!-- Loan List -->
    <ul class="divide-y">
      <li
        v-for="loan in loans"
        :key="loan.id"
        class="py-4 flex justify-between items-center"
      >
        <div>
          <p class="font-medium">{{ loan.loanName }} – {{ formatCurrency(loan.remainingBalance) }} / {{ formatCurrency(loan.loanAmount) }}</p>
          <p class="text-sm text-gray-500">Due: {{ formatDate(loan.dueDate) }} • Rate: {{ loan.interestRate }}%</p>
          <p v-if="loan.isPaidOff" class="text-green-600 font-semibold">(Paid Off)</p>
        </div>
        <button @click="removeLoan(loan.id)" class="text-red-500 hover:text-red-700">
          Delete
        </button>
      </li>
    </ul>
  </div>
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
  resetForm();
}

async function removeLoan(id: number) {
  await deleteLoan(id);
  loans.value = loans.value.filter(l => l.id !== id);
}

function resetForm() {
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

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('hu-HU', { style: 'currency', currency: 'HUF', maximumFractionDigits: 0 }).format(amount);
}

function formatDate(date: string): string {
  return date.split('T')[0];
}
</script>
