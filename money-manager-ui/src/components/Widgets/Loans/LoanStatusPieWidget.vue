<template>
  <div>
    <PieChart v-if="loans.length" :segments="segments" />
    <p v-else class="text-text-muted text-center text-sm">{{ t('loan.status.empty') }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { Loan } from '../../../models/models';
import { chartColors } from '../../../utils/chartTheme';
import PieChart from '../../ui/PieChart.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps<{ loans: Loan[] }>();

const paidOffCount = computed(() => props.loans.filter((l) => l.isPaidOff).length);
const remainingCount = computed(() => props.loans.filter((l) => !l.isPaidOff).length);

const segments = computed(() => [
  { label: t('loan.paidOff'), value: paidOffCount.value, color: chartColors.primary },
  { label: t('loan.remaining'), value: remainingCount.value, color: chartColors.danger },
]);
</script>
