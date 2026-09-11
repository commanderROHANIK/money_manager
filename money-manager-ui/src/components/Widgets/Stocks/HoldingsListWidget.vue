<template>
  <ul>
    <ListRow v-for="stock in stocks" :key="stock.id">
      <template #title>
        <div class="flex items-center gap-2">
          <Badge variant="outline" mono>{{ stock.ticker }}</Badge>
          <span class="text-sm text-text-muted">{{ stock.sharesOwned }} shares</span>
        </div>
      </template>
      <template #subtitle>
        <span class="text-xs text-text-muted">
          Bought <span class="tabular-nums">{{ formatMoney(stock.purchasePrice, stock.currencyCode, moneyOptions) }}</span>
          · now <span class="tabular-nums">{{ formatMoney(stock.currentPrice, stock.currencyCode, moneyOptions) }}</span>
        </span>
      </template>
      <template #trailing>
        <div class="flex items-center gap-3">
          <div class="flex flex-col items-end gap-0.5">
            <span class="text-xs text-text-muted">Value</span>
            <span class="text-sm font-semibold tabular-nums">
              {{ formatMoney(stock.sharesOwned * stock.currentPrice, stock.currencyCode, moneyOptions) }}
            </span>
            <span
              class="text-xs tabular-nums"
              :class="{
                'text-primary-strong': gain(stock) > 0,
                'text-danger': gain(stock) < 0,
                'text-text-muted': gain(stock) === 0,
              }"
            >
              {{ formatMoney(gain(stock), stock.currencyCode, moneyOptions) }}
            </span>
          </div>
          <button
            class="text-danger hover:text-danger/70 transition"
            :aria-label="t('stock.delete', { ticker: stock.ticker })"
            @click="_deleteStock(stock.id)"
          >
            ➖
          </button>
        </div>
      </template>
    </ListRow>
  </ul>
</template>

<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { fetchStocks, deleteStock } from '../../../services/api';
import type { Stock } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import ListRow from '../../ui/ListRow.vue';
import Badge from '../../ui/Badge.vue';

const { t } = useI18n();

const moneyOptions = { maximumFractionDigits: 2 };

const stocks = ref<Stock[]>([]);

async function load() {
  stocks.value = await fetchStocks();
}

onMounted(load);

function gain(stock: Stock): number {
  return (stock.currentPrice - stock.purchasePrice) * stock.sharesOwned;
}

async function _deleteStock(id: number) {
  try {
    await deleteStock(id);
    await load();
  } catch (error) {
    console.error('Failed to delete stock:', error);
  }
}
</script>
