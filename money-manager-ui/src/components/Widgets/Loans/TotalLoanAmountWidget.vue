<template>
  <StatCard label="Total Loan Amount" :value="formattedTotal" />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { Loan } from '../../../models/models';
import StatCard from '../../ui/StatCard.vue';

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
