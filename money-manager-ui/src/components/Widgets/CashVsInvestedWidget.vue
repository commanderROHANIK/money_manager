<template>
  <div class="bg-white p-6 rounded-2xl shadow-md">
    <h2 class="text-lg font-semibold mb-4">Cash vs Invested</h2>
    <div class="space-y-2">
      <div class="flex justify-between">
        <span>Cash:</span>
        <span class="font-medium text-green-600">{{ formattedCash }}</span>
      </div>
      <div class="flex justify-between">
        <span>Invested:</span>
        <span class="font-medium text-blue-600">{{ formattedInvested }}</span>
      </div>
      <div class="flex justify-between font-semibold mt-4 border-t pt-2">
        <span>Total:</span>
        <span>{{ formattedTotal }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchBankAccountsTotalBalance, fetchStocks } from '../../services/api';
import type { Stock } from '../../models/models';

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
