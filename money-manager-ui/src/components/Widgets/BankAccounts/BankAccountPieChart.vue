<template>
  <PieChart :segments="segments" />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { BankAccount } from '../../../models/models';
import { chartCategoricalPalette } from '../../../utils/chartTheme';
import PieChart from '../../ui/PieChart.vue';

const props = defineProps<{
  accounts: BankAccount[];
}>();

const segments = computed(() => {
  const palette = chartCategoricalPalette();
  return props.accounts.map((account, index) => ({
    label: account.accountName,
    value: account.balance,
    color: palette[index % palette.length],
  }));
});
</script>
