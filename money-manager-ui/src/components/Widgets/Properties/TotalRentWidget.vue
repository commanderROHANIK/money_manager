<template>
  <StatCard :label="t('property.totalRent.label')" :value="formattedTotal" />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { PortfolioAnalytics } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import StatCard from '../../ui/StatCard.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps<{ portfolio: PortfolioAnalytics | null }>();

// The portfolio's own converted total (CurrencyRollup.Sum on the backend) rather than a
// client-side sum across whatever currencies the properties happen to be in — this used to add
// raw amounts across currencies as if they were the same unit. Blank, not a wrong number, when a
// rate is missing — matches CashVsInvestedWidget's own compact-tile convention; there is no room
// here for the full "add the rate in Settings" disclosure a bigger card can afford.
const formattedTotal = computed(() => {
  const total = props.portfolio?.totalMonthlyRent;
  if (total === null || total === undefined) return '—';
  return formatMoney(total, props.portfolio?.currency ?? 'EUR');
});
</script>
