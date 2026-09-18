<template>
  <PieChart :segments="segments" />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { Loan } from '../../../models/models';
import { chartCategoricalPalette } from '../../../utils/chartTheme';
import PieChart from '../../ui/PieChart.vue';

const props = defineProps<{
  accounts: Loan[];
}>();

const segments = computed(() => {
  const palette = chartCategoricalPalette();
  return props.accounts.map((account, index) => ({
    label: account.loanName,
    value: account.loanAmount,
    color: palette[index % palette.length],
  }));
});
</script>
