<template>
  <table class="w-full text-sm text-left">
    <thead>
      <tr class="border-b font-semibold">
        <th>Ticker</th>
        <th>Shares</th>
        <th>Purchase Price</th>
        <th>Current Price</th>
        <th>Total Value</th>
        <th>Gain/Loss</th>
      </tr>
    </thead>
    <tbody>
      <tr
        v-for="stock in stocks"
        :key="stock.id"
        class="border-b hover:bg-gray-50"
      >
        <td>{{ stock.ticker }}</td>
        <td>{{ stock.sharesOwned }}</td>
        <td>{{ formatCurrency(stock.purchasePrice) }}</td>
        <td>{{ formatCurrency(stock.currentPrice) }}</td>
        <td>{{ formatCurrency(stock.sharesOwned * stock.currentPrice) }}</td>
        <td :class="{
          'text-green-600': gain(stock) > 0,
          'text-red-600': gain(stock) < 0,
        }">
          {{ formatCurrency(gain(stock)) }}
        </td>
      </tr>
    </tbody>
  </table>
</template>

<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { fetchStocks } from '../../../services/api';
import type { Stock } from '../../../models/models';

const stocks = ref<Stock[]>([]);

onMounted(async () => {
  stocks.value = await fetchStocks();
});

function gain(stock: Stock): number {
  return (stock.currentPrice - stock.purchasePrice) * stock.sharesOwned;
}

function formatCurrency(value: number): string {
  return value.toLocaleString('hu-HU', {
    style: 'currency',
    currency: 'HUF',
    maximumFractionDigits: 2,
  });
}
</script>
