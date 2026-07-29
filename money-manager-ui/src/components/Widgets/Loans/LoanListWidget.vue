<template>
  <div>
    <h2 class="text-lg font-semibold mb-4">All Loans</h2>

    <p v-if="loans.length === 0" class="text-sm text-gray-500">No loans yet.</p>

    <ul v-else class="divide-y">
      <li
        v-for="loan in loans"
        :key="loan.id"
        class="py-4 flex justify-between items-center"
      >
        <div>
          <p class="font-medium">
            {{ loan.loanName }} – {{ formatMoney(loan.remainingBalance, loan.currencyCode) }}
            / {{ formatMoney(loan.loanAmount, loan.currencyCode) }}
          </p>
          <p class="text-sm text-gray-500">Due: {{ formatDate(loan.dueDate) }} • Rate: {{ loan.interestRate }}%</p>
          <p v-if="loan.isPaidOff" class="text-green-600 font-semibold">(Paid Off)</p>
        </div>
        <button @click="confirmDelete(loan)" class="text-red-500 hover:text-red-700">
          Delete
        </button>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { Loan } from '../../../models/models';
import { formatMoney } from '../../../utils/money';

defineProps<{
  loans: Loan[];
}>();

const emit = defineEmits<{ (e: 'delete-loan', id: number): void }>();

function confirmDelete(loan: Loan) {
  if (window.confirm(`Delete "${loan.loanName}"?`)) {
    emit('delete-loan', loan.id);
  }
}

function formatDate(date: string): string {
  return date.split('T')[0];
}
</script>
