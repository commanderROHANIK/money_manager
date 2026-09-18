<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('loan.status.title') }}</h2>
    <PieChart v-if="loans.length" :segments="segments">
      <template #center>
        <span class="font-heading text-xl font-extrabold tabular-nums">{{ loans.length }}</span>
      </template>
    </PieChart>
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
