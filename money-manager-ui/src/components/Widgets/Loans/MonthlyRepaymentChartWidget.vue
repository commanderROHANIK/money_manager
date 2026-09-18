<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('loan.monthlyRepayment.title') }}</h2>
    <PieChart :segments="segments">
      <template #center>
        <span class="font-heading text-xs font-extrabold tabular-nums leading-tight">{{ formattedTotal }}</span>
        <span class="text-[9px] font-semibold text-text-muted uppercase tracking-wide">{{ t('loan.monthlyRepayment.total') }}</span>
      </template>
    </PieChart>
    <p v-if="missingRateMessage" class="text-xs text-accent-strong mt-2">{{ missingRateMessage }}</p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import type { Loan, LoanAmountSummary } from '../../../models/models';
import { chartCategoricalPalette } from '../../../utils/chartTheme';
import { fetchLoansTotalAmount } from '../../../services/api';
import { formatMoney } from '../../../utils/money';
import { useRateDisclosure } from '../../../composables/useRateDisclosure';
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

// Fetched independently of the `accounts` prop above, same reason as BankAccountPieChart: the
// center figure needs the currency-safe rollup, not a client-side sum of the prop's raw amounts.
const summary = ref<LoanAmountSummary | null>(null);

onMounted(async () => {
  try {
    summary.value = await fetchLoansTotalAmount();
  } catch (err) {
    console.error('Failed to fetch loan total:', err);
  }
});

const formattedTotal = computed(() => {
  const s = summary.value;
  if (!s) return '';
  if (s.totalAmount === null) return '—';
  return formatMoney(s.totalAmount, s.currency);
});

const { missingRateMessage } = useRateDisclosure(computed(() => summary.value));
</script>
