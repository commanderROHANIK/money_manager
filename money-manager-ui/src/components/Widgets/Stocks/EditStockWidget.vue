<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('stock.edit.title') }}</h2>

    <form class="space-y-3" @submit.prevent="submit">
      <BaseInput
        v-model.trim="form.ticker"
        :label="t('stock.add.ticker')"
        :placeholder="t('stock.add.ticker')"
        required
      />

      <BaseInput
        v-model.number="form.sharesOwned"
        :label="t('stock.add.sharesOwned')"
        :placeholder="t('stock.add.sharesOwned')"
        type="number"
        min="0"
        step="1"
        required
      />

      <BaseInput
        v-model.number="form.purchasePrice"
        :label="t('stock.add.purchasePrice')"
        :placeholder="t('stock.add.purchasePrice')"
        type="number"
        min="0"
        step="0.01"
        required
      />

      <BaseInput
        v-model.number="form.currentPrice"
        :label="t('stock.add.currentPrice')"
        :placeholder="t('stock.add.currentPrice')"
        type="number"
        min="0"
        step="0.01"
        required
      />

      <div>
        <BaseInput v-model="form.purchaseDate" type="date" :label="t('stock.add.purchaseDate')" required />
        <p class="text-xs text-text-muted mt-1">{{ t('stock.add.purchaseDateHint') }}</p>
      </div>

      <div>
        <BaseSelect v-model="form.currencyCode" :label="t('stock.add.currency')">
          <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
        </BaseSelect>
        <p class="text-xs text-text-muted mt-1">{{ t('stock.add.currencyHint') }}</p>
      </div>

      <BaseButton type="submit">{{ t('stock.edit.submit') }}</BaseButton>
    </form>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import { useI18n } from 'vue-i18n';
import type { Stock } from '../../../models/models';
import { CURRENCIES } from '../../../utils/currencies';
import BaseInput from '../../ui/BaseInput.vue';
import BaseSelect from '../../ui/BaseSelect.vue';
import BaseButton from '../../ui/BaseButton.vue';

const { t } = useI18n();

const props = defineProps<{ stock: Stock }>();
const emit = defineEmits<{ (e: 'update', payload: Stock): void }>();

function fromStock(stock: Stock) {
  return {
    ticker: stock.ticker,
    sharesOwned: stock.sharesOwned,
    purchasePrice: stock.purchasePrice,
    currentPrice: stock.currentPrice,
    purchaseDate: stock.purchaseDate.slice(0, 10),
    currencyCode: stock.currencyCode,
  };
}

const form = reactive(fromStock(props.stock));

function submit() {
  emit('update', { id: props.stock.id, ...form });
}
</script>
