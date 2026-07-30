<template>
  <div class="p-6">
    <h2 class="font-heading text-lg font-bold mb-2">Next Due Repayments</h2>
    <ul v-if="nextPayments.length">
      <ListRow v-for="loan in nextPayments" :key="loan.id">
        <template #title>
          <p class="font-medium">{{ loan.loanName }}</p>
        </template>
        <template #subtitle>
          <p class="text-sm text-text-muted">Due: {{ formatDate(loan.dueDate) }}</p>
        </template>
        <template #trailing>
          <p class="font-semibold tabular-nums">{{ formatCurrency(loan.remainingBalance) }}</p>
        </template>
      </ListRow>
    </ul>
    <p v-else class="text-text-muted">No upcoming repayments.</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { Loan } from '../../../models/models';
import ListRow from '../../ui/ListRow.vue';

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
