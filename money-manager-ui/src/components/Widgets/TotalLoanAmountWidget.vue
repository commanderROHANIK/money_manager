<template>
  <div class="p-6">
    <h2 class="text-lg font-semibold mb-2">Total Loan Amount</h2>
    <p class="text-2xl font-bold text-blue-600">
      {{ formattedTotal }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { Loan } from '../../models/models';

const props = defineProps<{
  loans: Loan[];
}>();

const totalAmount = computed(() =>
  props.loans.reduce((sum, loan) => sum + loan.loanAmount, 0)
);

const formattedTotal = computed(() =>
  new Intl.NumberFormat('hu-HU', {
    style: 'currency',
    currency: 'HUF',
    maximumFractionDigits: 0,
  }).format(totalAmount.value)
);
</script>
