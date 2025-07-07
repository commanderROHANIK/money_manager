<template>
  <div class="p-6">
    <h2 class="text-lg font-semibold mb-2">Next Due Repayments</h2>
    <ul v-if="nextPayments.length" class="space-y-2 text-sm">
      <li v-for="loan in nextPayments" :key="loan.id" class="flex justify-between">
        <div>
          <p class="font-medium">{{ loan.loanName }}</p>
          <p class="text-gray-500">Due: {{ formatDate(loan.dueDate) }}</p>
        </div>
        <p class="font-semibold">{{ formatCurrency(loan.remainingBalance) }}</p>
      </li>
    </ul>
    <p v-else class="text-gray-500">No upcoming repayments.</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { Loan } from '../../models/models';

const props = defineProps<{ loans: Loan[] }>();

const nextPayments = computed(() => {
  const today = new Date();

  return props.loans
    .filter(l => {
      const due = new Date(l.dueDate);
      return !l.isPaidOff && due >= today;
    })
    .sort((a, b) => new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime()) // ascending order
    .slice(0, 3);
});

function formatDate(date: string): string {
  return date.split('T')[0];
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('hu-HU', {
    style: 'currency',
    currency: 'HUF',
    maximumFractionDigits: 0,
  }).format(amount);
}
</script>
