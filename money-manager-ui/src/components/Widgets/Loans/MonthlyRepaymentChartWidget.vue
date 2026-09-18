<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('loan.monthlyRepayment.title') }}</h2>
    <PieChart :segments="segments" />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { Loan } from '../../../models/models';
import { chartCategoricalPalette } from '../../../utils/chartTheme';
import PieChart from '../../ui/PieChart.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

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
