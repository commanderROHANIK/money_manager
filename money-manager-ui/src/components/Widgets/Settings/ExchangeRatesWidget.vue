<template>
  <BaseCard :title="t('settings.ratesTitle')">
    <p class="mb-4 text-sm text-text-muted">
      {{ t('settings.ratesIntro') }}
    </p>

    <form class="flex flex-wrap items-end gap-3" @submit.prevent="save">
      <BaseSelect v-model="from" :label="t('settings.oneUnitOf')" class="w-32">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <BaseSelect v-model="to" :label="t('settings.isWorthIn')" class="w-32">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <BaseInput v-model.number="rate" :label="t('settings.amount')" type="number" step="any" min="0" class="w-40" />

      <BaseInput v-model="asOf" :label="t('settings.asOf')" type="date" class="w-44" />

      <BaseButton type="submit" :disabled="saving">{{ t('settings.saveRate') }}</BaseButton>
    </form>

    <p v-if="error" class="mt-3 text-sm text-danger">{{ error }}</p>

    <EmptyState
      v-if="rates.length === 0"
      class="mt-4"
      :title="t('settings.noRates')"
      :description="t('settings.noRatesHint')"
    />

    <ul v-else class="mt-4 divide-y divide-border">
      <li v-for="entry in rates" :key="entry.id" class="flex items-center justify-between py-2.5 text-sm">
        <span class="tabular-nums">
          1 {{ entry.baseCurrency }} = {{ entry.rate }} {{ entry.quoteCurrency }}
        </span>
        <span class="flex items-center gap-3">
          <span class="text-xs text-text-muted">{{
            t('settings.recorded', { date: formatDate(entry.asOf) })
          }}</span>
          <BaseButton variant="danger" size="sm" @click="remove(entry)">{{ t('settings.remove') }}</BaseButton>
        </span>
      </li>
    </ul>
  </BaseCard>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import type { ExchangeRate } from '../../../models/models';
import {
  deleteExchangeRate,
  fetchExchangeRates,
  upsertExchangeRate,
} from '../../../services/exchangeRateApi';
import { CURRENCIES } from '../../../utils/currencies';
import { formatDate } from '../../../utils/labels';
import BaseButton from '../../ui/BaseButton.vue';
import BaseCard from '../../ui/BaseCard.vue';
import BaseInput from '../../ui/BaseInput.vue';
import BaseSelect from '../../ui/BaseSelect.vue';
import EmptyState from '../../ui/EmptyState.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const emit = defineEmits<{ (e: 'changed'): void }>();

const rates = ref<ExchangeRate[]>([]);
const from = ref<string>('EUR');
const to = ref<string>('HUF');
const rate = ref<number | null>(null);
const asOf = ref<string>(new Date().toISOString().slice(0, 10));
const saving = ref(false);
const error = ref('');

onMounted(load);

async function load() {
  try {
    rates.value = await fetchExchangeRates();
  } catch (err) {
    console.error('Failed to load exchange rates:', err);
  }
}

async function save() {
  error.value = '';

  if (from.value === to.value) {
    error.value = t('settings.sameCurrency');
    return;
  }

  if (rate.value === null || rate.value <= 0) {
    error.value = t('settings.rateTooSmall');
    return;
  }

  saving.value = true;

  try {
    await upsertExchangeRate(from.value, to.value, rate.value, asOf.value);
    rate.value = null;
    await load();
    emit('changed');
  } catch (err) {
    console.error('Failed to save exchange rate:', err);
    error.value = t('settings.rateSaveFailed');
  } finally {
    saving.value = false;
  }
}

async function remove(entry: ExchangeRate) {
  try {
    await deleteExchangeRate(entry.baseCurrency, entry.quoteCurrency);
    await load();
    emit('changed');
  } catch (err) {
    console.error('Failed to remove exchange rate:', err);
    error.value = t('settings.rateRemoveFailed');
  }
}
</script>
