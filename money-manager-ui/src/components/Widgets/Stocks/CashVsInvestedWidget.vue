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
import { fetchBankAccountsTotalBalance, fetchStocks } from '../../../services/api';
import type { BankBalanceSummary, Stock } from '../../../models/models';
import { formatMoney, sumSameCurrency } from '../../../utils/money';
import BaseCard from '../../ui/BaseCard.vue';

const cash = ref<BankBalanceSummary | null>(null);
const stocks = ref<Stock[]>([]);

onMounted(async () => {
  try {
    cash.value = await fetchBankAccountsTotalBalance();
  } catch (error) {
    console.error('Failed to fetch bank balance:', error);
  }

  try {
    stocks.value = await fetchStocks();
  } catch (error) {
    console.error('Failed to fetch stocks:', error);
  }
});

const invested = computed(() =>
  sumSameCurrency(
    stocks.value,
    (stock) => stock.sharesOwned * stock.currentPrice,
    (stock) => stock.currencyCode
  )
);

const formattedCash = computed(() => {
  const s = cash.value;
  if (!s) return '';
  return s.totalBalance === null ? '—' : formatMoney(s.totalBalance, s.currency);
});

const formattedInvested = computed(() =>
  invested.value.mixed ? '—' : formatMoney(invested.value.total, invested.value.currency)
);

/**
 * Cash and holdings only add up when they are in the same currency and both figures are known.
 * Adding a EUR cash balance to HUF holdings would produce a number that looks like net worth and
 * is wrong by two orders of magnitude, which is the exact defect this rollup work exists to end.
 */
const combinable = computed(() => {
  const s = cash.value;
  return (
    s !== null &&
    s.totalBalance !== null &&
    !invested.value.mixed &&
    invested.value.currency === s.currency
  );
});

const formattedTotal = computed(() => {
  const s = cash.value;
  if (!combinable.value || !s || s.totalBalance === null) return '—';
  return formatMoney(s.totalBalance + invested.value.total, s.currency);
});

const totalNote = computed(() => {
  if (combinable.value || !cash.value) return '';
  if (invested.value.mixed) return 'Holdings span several currencies, so they are not totalled here.';
  if (cash.value.totalBalance === null) return 'Cash cannot be totalled without an exchange rate.';
  return `Cash is in ${cash.value.currency} and holdings are in ${invested.value.currency}, so they are not added together.`;
});
</script>
