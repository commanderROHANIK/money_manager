<template>
  <BaseCard title="Cash vs Invested">
    <div class="space-y-2 text-sm">
      <div class="flex justify-between">
        <span class="text-text-muted">Cash:</span>
        <span class="font-medium text-accent-strong tabular-nums">{{ formattedCash }}</span>
      </div>
      <div class="flex justify-between">
        <span class="text-text-muted">Invested:</span>
        <span class="font-medium text-primary-strong tabular-nums">{{ formattedInvested }}</span>
      </div>
      <div class="flex justify-between font-semibold mt-4 border-t border-border pt-2">
        <span>Total:</span>
        <span class="tabular-nums">{{ formattedTotal }}</span>
      </div>
      <p v-if="totalNote" class="text-xs text-text-muted">{{ totalNote }}</p>
    </div>
  </BaseCard>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchBankAccountsTotalBalance, fetchStocksTotalValue } from '../../../services/api';
import type { BankBalanceSummary, StockValueSummary } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import BaseCard from '../../ui/BaseCard.vue';

const cash = ref<BankBalanceSummary | null>(null);
const invested = ref<StockValueSummary | null>(null);

onMounted(async () => {
  try {
    cash.value = await fetchBankAccountsTotalBalance();
  } catch (error) {
    console.error('Failed to fetch bank balance:', error);
  }

  try {
    invested.value = await fetchStocksTotalValue();
  } catch (error) {
    console.error('Failed to fetch stock value:', error);
  }
});

const formattedCash = computed(() => {
  const s = cash.value;
  if (!s) return '';
  return s.totalBalance === null ? '—' : formatMoney(s.totalBalance, s.currency);
});

// The holdings' own converted total (CurrencyRollup.Sum on the backend) rather than a
// client-side sum across whatever currencies the holdings happen to be in — this used to add raw
// amounts across currencies as if they were the same unit, and blank out entirely rather than
// convert whenever they were mixed. A rate on record now produces a real total here instead.
const formattedInvested = computed(() => {
  const i = invested.value;
  if (!i || i.totalValue === null) return '—';
  return formatMoney(i.totalValue, i.currency);
});

/**
 * Cash and holdings only add up when they are in the same currency and both figures are known.
 * Adding a EUR cash balance to HUF holdings would produce a number that looks like net worth and
 * is wrong by two orders of magnitude, which is the exact defect this rollup work exists to end.
 */
const combinable = computed(() => {
  const c = cash.value;
  const i = invested.value;
  return c !== null && c.totalBalance !== null && i !== null && i.totalValue !== null && i.currency === c.currency;
});

const formattedTotal = computed(() => {
  const c = cash.value;
  const i = invested.value;
  if (!combinable.value || !c || c.totalBalance === null || !i || i.totalValue === null) return '—';
  return formatMoney(c.totalBalance + i.totalValue, c.currency);
});

const totalNote = computed(() => {
  const c = cash.value;
  const i = invested.value;
  if (combinable.value || !c || !i) return '';
  if (i.totalValue === null) return 'Holdings cannot be totalled without an exchange rate.';
  if (c.totalBalance === null) return 'Cash cannot be totalled without an exchange rate.';
  return `Cash is in ${c.currency} and holdings are in ${i.currency}, so they are not added together.`;
});
</script>
