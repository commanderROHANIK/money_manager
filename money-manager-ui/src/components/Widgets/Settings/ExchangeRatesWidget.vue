<template>
  <BaseCard title="Exchange rates">
    <p class="mb-4 text-sm text-text-muted">
      Rates you enter yourself — nothing is fetched, so a converted total is only ever as good as
      what is on this list, and every one is shown with the date you recorded it. Entering one
      direction is enough; the other is read off the same row.
    </p>

    <form class="flex flex-wrap items-end gap-3" @submit.prevent="save">
      <BaseSelect v-model="from" label="One unit of" class="w-32">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <BaseSelect v-model="to" label="Is worth, in" class="w-32">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <BaseInput v-model.number="rate" label="Amount" type="number" step="any" min="0" class="w-40" />

      <BaseInput v-model="asOf" label="As of" type="date" class="w-44" />

      <BaseButton type="submit" :disabled="saving">Save rate</BaseButton>
    </form>

    <p v-if="error" class="mt-3 text-sm text-danger">{{ error }}</p>

    <EmptyState
      v-if="rates.length === 0"
      class="mt-4"
      title="No rates yet"
      description="Add one above and portfolios spanning currencies will start reporting totals."
    />

    <ul v-else class="mt-4 divide-y divide-border">
      <li v-for="entry in rates" :key="entry.id" class="flex items-center justify-between py-2.5 text-sm">
        <span class="tabular-nums">
          1 {{ entry.baseCurrency }} = {{ entry.rate }} {{ entry.quoteCurrency }}
        </span>
        <span class="flex items-center gap-3">
          <span class="text-xs text-text-muted">recorded {{ formatDate(entry.asOf) }}</span>
          <BaseButton variant="danger" size="sm" @click="remove(entry)">Remove</BaseButton>
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
    error.value = 'A currency is always worth one of itself; pick two different currencies.';
    return;
  }

  if (rate.value === null || rate.value <= 0) {
    error.value = 'Enter a rate greater than zero.';
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
    error.value = 'Could not save that rate. Please try again.';
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
    error.value = 'Could not remove that rate. Please try again.';
  }
}
</script>
