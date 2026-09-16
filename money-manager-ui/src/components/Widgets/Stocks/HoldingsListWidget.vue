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
            class="text-text-muted hover:text-text transition"
            :aria-label="t('stock.edit.open', { ticker: stock.ticker })"
            @click="editingStock = stock"
          >
            ✏️
          </button>
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

  <!-- Edit Modal -->
  <div
    v-if="editingStock"
    role="dialog"
    aria-modal="true"
    :aria-label="t('stock.edit.title')"
    class="fixed inset-0 bg-black/40 flex justify-center items-center z-50"
  >
    <div class="bg-surface border border-border rounded-lg shadow-card p-6 w-96">
      <EditStockWidget :stock="editingStock" @update="_updateStock" />
      <BaseButton variant="secondary" class="mt-3" block @click="editingStock = null">
        {{ t('stock.close') }}
      </BaseButton>
    </div>
  </div>
</template>

<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { fetchStocks, updateStock, deleteStock } from '../../../services/api';
import type { Stock } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import ListRow from '../../ui/ListRow.vue';
import Badge from '../../ui/Badge.vue';
import BaseButton from '../../ui/BaseButton.vue';
import EditStockWidget from './EditStockWidget.vue';

const { t } = useI18n();

const moneyOptions = { maximumFractionDigits: 2 };

const stocks = ref<Stock[]>([]);
const editingStock = ref<Stock | null>(null);

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

async function _updateStock(payload: Stock) {
  try {
    await updateStock(payload.id, payload);
    await load();
    editingStock.value = null;
  } catch (error) {
    console.error('Failed to update stock:', error);
  }
}
</script>
