<template>
  <ul>
    <ListRow v-for="stock in stocks" :key="stock.id">
      <template #title>
        <div class="flex items-center gap-2">
          <Badge variant="outline">{{ stock.ticker }}</Badge>
          <span class="text-sm text-text-muted">{{ stock.sharesOwned }} shares</span>
        </div>
      </template>
      <template #subtitle>
        <span class="text-xs text-text-muted tabular-nums">
          {{ formatCurrency(stock.purchasePrice) }} &rarr; {{ formatCurrency(stock.currentPrice) }}
        </span>
      </template>
      <template #trailing>
        <div class="flex flex-col items-end gap-0.5">
          <span class="text-sm font-semibold tabular-nums">{{ formatCurrency(stock.sharesOwned * stock.currentPrice) }}</span>
          <span
            class="text-xs tabular-nums"
            :class="{
              'text-primary': gain(stock) > 0,
              'text-danger': gain(stock) < 0,
              'text-text-muted': gain(stock) === 0,
            }"
          >
            {{ formatCurrency(gain(stock)) }}
          </span>
        </div>
      </template>
    </ListRow>
  </ul>
</template>

<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { fetchStocks } from '../../../services/api';
import type { Stock } from '../../../models/models';
import ListRow from '../../ui/ListRow.vue';
import Badge from '../../ui/Badge.vue';

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
