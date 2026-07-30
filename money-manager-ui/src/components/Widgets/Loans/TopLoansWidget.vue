<template>
  <div class="p-6">
    <h2 class="font-heading text-lg font-bold mb-2">Top Loans</h2>
    <ul v-if="topLoans.length">
      <ListRow v-for="loan in topLoans" :key="loan.id">
        <template #title>
          <p class="font-medium">{{ loan.loanName }}</p>
        </template>
        <template #subtitle>
          <p class="text-sm text-text-muted">Remaining: {{ formatCurrency(loan.remainingBalance) }}</p>
        </template>
        <template #trailing>
          <p class="font-semibold text-danger tabular-nums">{{ formatCurrency(loan.loanAmount) }}</p>
        </template>
      </ListRow>
    </ul>
    <p v-else class="text-text-muted">No loans available.</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { Loan } from '../../../models/models';
import ListRow from '../../ui/ListRow.vue';

const props = defineProps<{ loans: Loan[] }>();

const topLoans = computed(() => {
  return [...props.loans]
    .filter(l => !l.isPaidOff)
    .sort((a, b) => b.remainingBalance - a.remainingBalance)
    .slice(0, 3);
});

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('hu-HU', {
    style: 'currency',
    currency: 'HUF',
    maximumFractionDigits: 0,
  }).format(amount);
}
</script>
