<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">All Loans</h2>

    <p v-if="loans.length === 0" class="text-sm text-text-muted">No loans yet.</p>

    <ul v-else>
      <ListRow v-for="loan in loans" :key="loan.id">
        <template #title>
          <p class="font-medium tabular-nums">
            {{ loan.loanName }} – {{ formatMoney(loan.remainingBalance, loan.currencyCode) }}
            / {{ formatMoney(loan.loanAmount, loan.currencyCode) }}
          </p>
        </template>
        <template #subtitle>
          <p class="text-sm text-text-muted">Due: {{ formatDate(loan.dueDate) }} • Rate: {{ loan.interestRate }}%</p>
          <Badge v-if="loan.isPaidOff" variant="primary">Paid Off</Badge>
        </template>
        <template #trailing>
          <button @click="confirmDelete(loan)" class="text-sm font-semibold text-danger hover:text-danger/80">
            Delete
          </button>
        </template>
      </ListRow>
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { Loan } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import ListRow from '../../ui/ListRow.vue';
import Badge from '../../ui/Badge.vue';

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
