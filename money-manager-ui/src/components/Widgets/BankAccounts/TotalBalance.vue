<template>
  <StatCard label="Total Balance" :value="formattedBalance" :delta="note" :delta-positive="!summary?.missingRates.length" />
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchBankAccountsTotalBalance } from '../../../services/api';
import type { BankBalanceSummary } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import StatCard from '../../ui/StatCard.vue';

const summary = ref<BankBalanceSummary | null>(null);

onMounted(async () => {
  try {
    summary.value = await fetchBankAccountsTotalBalance();
  } catch (err) {
    console.error('Failed to fetch balance:', err);
  }
});

/**
 * Rendered in the currency the server says the figure is in, rather than the hardcoded HUF this
 * used to force onto every balance. A null total means accounts span currencies with no rate to
 * combine them — unknown, which is not the same as zero.
 */
const formattedBalance = computed(() => {
  const s = summary.value;
  if (!s) return '';
  if (s.totalBalance === null) return '—';
  return formatMoney(s.totalBalance, s.currency);
});

const note = computed(() => {
  const s = summary.value;
  if (!s) return '';

  if (s.missingRates.length > 0) {
    return `No rate for ${s.missingRates.map((p) => `${p.from}→${p.to}`).join(', ')}`;
  }

  return s.converted ? `Converted to ${s.currency} at your rates` : '';
});
</script>
