<template>
  <div class="p-6">
    <h2 class="text-lg font-semibold mb-2">Top Loans</h2>
    <ul v-if="topLoans.length" class="space-y-2 text-sm">
      <li v-for="loan in topLoans" :key="loan.id" class="flex justify-between">
        <div>
          <p class="font-medium">{{ loan.loanName }}</p>
          <p class="text-gray-500">Remaining: {{ formatCurrency(loan.remainingBalance) }}</p>
        </div>
        <p class="font-semibold text-red-500">{{ formatCurrency(loan.loanAmount) }}</p>
      </li>
    </ul>
    <p v-else class="text-gray-500">No loans available.</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { Loan } from '../../../models/models';

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
