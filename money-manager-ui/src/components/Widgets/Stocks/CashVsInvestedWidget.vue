<template>
  <BaseCard title="Cash vs Invested">
    <div class="space-y-2 text-sm">
      <div class="flex justify-between">
        <span class="text-text-muted">Cash:</span>
        <span class="font-medium text-primary tabular-nums">{{ formattedCash }}</span>
      </div>
      <div class="flex justify-between">
        <span class="text-text-muted">Invested:</span>
        <span class="font-medium text-primary-strong tabular-nums">{{ formattedInvested }}</span>
      </div>
      <div class="flex justify-between font-semibold mt-4 border-t border-border pt-2">
        <span>Total:</span>
        <span class="tabular-nums">{{ formattedTotal }}</span>
      </div>
    </div>
  </BaseCard>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchBankAccountsTotalBalance, fetchStocks } from '../../../services/api';
import type { Stock } from '../../../models/models';
import BaseCard from '../../ui/BaseCard.vue';

const cash = ref<number | null>(null);
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
  stocks.value.reduce((sum, stock) => sum + stock.sharesOwned * stock.currentPrice, 0)
);

const total = computed(() =>
  (cash.value ?? 0) + invested.value
);

const formatter = new Intl.NumberFormat('hu-HU', {
  style: 'currency',
  currency: 'HUF',
  maximumFractionDigits: 0,
});

const formattedCash = computed(() =>
  cash.value !== null ? formatter.format(cash.value) : ''
);

const formattedInvested = computed(() =>
  formatter.format(invested.value)
);

const formattedTotal = computed(() =>
  formatter.format(total.value)
);
</script>
