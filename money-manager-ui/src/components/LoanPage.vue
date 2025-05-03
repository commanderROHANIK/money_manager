<template>
    <div class="p-4">
      <h2 class="text-2xl font-bold mb-4">Loans</h2>
  
      <form @submit.prevent="_createLoan" class="mb-4">
        <input v-model="newLoan.loanName" placeholder="Loan Name" class="p-2 border rounded mr-2" required />
        <input v-model.number="newLoan.loanAmount" placeholder="Amount" class="p-2 border rounded mr-2" type="number" required />
        <input v-model.number="newLoan.remainingBalance" placeholder="Remaining" class="p-2 border rounded mr-2" type="number" required />
        <input v-model.number="newLoan.interestRate" placeholder="Interest Rate (%)" class="p-2 border rounded mr-2" type="number" required />
        <input v-model="newLoan.dueDate" type="date" class="p-2 border rounded mr-2" required />
        <label><input v-model="newLoan.isPaidOff" type="checkbox" class="mr-1" />Paid Off</label>
        <button type="submit" class="ml-2 bg-blue-500 text-white p-2 rounded">Add</button>
      </form>
  
      <ul>
        <li v-for="loan in loans" :key="loan.id" class="mb-2 bg-white p-4 rounded shadow">
          <strong>{{ loan.loanName }}</strong> – {{ loan.remainingBalance }} / {{ loan.loanAmount }} @ {{ loan.interestRate }}% – Due: {{ loan.dueDate.split('T')[0] }}
          <span v-if="loan.isPaidOff" class="text-green-600 font-semibold ml-2">(Paid Off)</span>
          <button @click="_deleteLoan(loan.id)" class="ml-4 text-red-500">Delete</button>
        </li>
      </ul>
    </div>
  </template>
  
  <script setup lang="ts">
  import { ref, onMounted } from 'vue';
  import { fetchLoans, createLoan, deleteLoan } from '../services/api';
  import type { Loan } from '../models/models';
  
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
  
  async function _createLoan() {
    const loan = await createLoan(newLoan.value);
    loans.value.push(loan);
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
  
  async function _deleteLoan(id: number) {
    await deleteLoan(id);
    loans.value = loans.value.filter(l => l.id !== id);
  }
  </script>
  