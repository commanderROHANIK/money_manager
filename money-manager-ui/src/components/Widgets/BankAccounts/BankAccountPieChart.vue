<template>
  <div>
    <PieChart :segments="segments">
      <template #center>
        <span class="font-heading text-xs font-extrabold tabular-nums leading-tight">{{ formattedTotal }}</span>
        <span class="text-[9px] font-semibold text-text-muted uppercase tracking-wide">{{ t('bankAccount.total') }}</span>
      </template>
    </PieChart>
    <p v-if="missingRateMessage" class="text-xs text-accent-strong mt-2">{{ missingRateMessage }}</p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import type { BankAccount, BankBalanceSummary } from '../../../models/models';
import { chartCategoricalPalette } from '../../../utils/chartTheme';
import { fetchBankAccountsTotalBalance } from '../../../services/api';
import { formatMoney } from '../../../utils/money';
import { useRateDisclosure } from '../../../composables/useRateDisclosure';
import PieChart from '../../ui/PieChart.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

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

// Fetched independently of the `accounts` prop above (which only carries what each slice needs)
// because the center figure has to be the same currency-safe rollup every other total in the app
// uses — summing the prop's raw balances here would reintroduce the exact bug that rollup exists
// to prevent.
const summary = ref<BankBalanceSummary | null>(null);

onMounted(async () => {
  try {
    summary.value = await fetchBankAccountsTotalBalance();
  } catch (err) {
    console.error('Failed to fetch balance:', err);
  }
});

const formattedTotal = computed(() => {
  const s = summary.value;
  if (!s) return '';
  if (s.totalBalance === null) return '—';
  return formatMoney(s.totalBalance, s.currency);
});

const { missingRateMessage } = useRateDisclosure(computed(() => summary.value));
</script>
