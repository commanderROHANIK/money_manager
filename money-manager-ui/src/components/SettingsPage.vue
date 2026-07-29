<template>
  <div class="p-4 space-y-4 max-w-3xl">
    <h1 class="text-2xl font-bold">Settings</h1>

    <!-- Base currency -->
    <div class="bg-white dark:bg-gray-800 p-4 rounded-2xl shadow-md">
      <h2 class="text-xl font-semibold mb-1">Base currency</h2>
      <p class="text-xs text-gray-500 mb-3">
        Portfolio totals are reported in this currency. Each property keeps its own currency
        — only the consolidated figures are converted.
      </p>

      <div class="flex gap-2 items-center">
        <select v-model="baseCurrency" class="p-2 border rounded w-32">
          <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
        </select>
        <button
          class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded disabled:opacity-50"
          :disabled="saving || baseCurrency === savedBaseCurrency"
          @click="saveBaseCurrency"
        >
          Save
        </button>
        <span v-if="savedMessage" class="text-sm text-green-700">{{ savedMessage }}</span>
      </div>
    </div>

    <!-- Exchange rates -->
    <div class="bg-white dark:bg-gray-800 p-4 rounded-2xl shadow-md">
      <h2 class="text-xl font-semibold mb-1">Exchange rates</h2>
      <p class="text-xs text-gray-500 mb-3">
        Only one direction of each pair is needed — the reverse is derived. Pairs that are
        not stored directly are crossed through EUR.
      </p>

      <form @submit.prevent="addRate" class="flex flex-wrap gap-2 mb-4 items-center">
        <select v-model="form.fromCurrency" class="p-2 border rounded w-24">
          <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
        </select>
        <span class="text-gray-500">→</span>
        <select v-model="form.toCurrency" class="p-2 border rounded w-24">
          <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
        </select>
        <input
          v-model.number="form.rate"
          type="number"
          step="any"
          min="0"
          placeholder="Rate"
          class="p-2 border rounded w-32"
          required
        />
        <input v-model="form.asOf" type="date" class="p-2 border rounded" />
        <button type="submit" class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded">
          Save rate
        </button>
      </form>

      <p v-if="error" class="text-sm text-red-600 mb-3">{{ error }}</p>

      <p v-if="rates.length === 0" class="text-sm text-gray-500">
        No rates stored. A single-currency portfolio does not need any.
      </p>

      <ul v-else class="divide-y text-sm">
        <li v-for="rate in rates" :key="rate.id" class="py-2 flex justify-between items-center gap-3">
          <span>
            1 {{ rate.fromCurrency }} =
            <strong>{{ rate.rate.toLocaleString() }}</strong> {{ rate.toCurrency }}
          </span>
          <span class="flex items-center gap-3 text-gray-500 whitespace-nowrap">
            <span>{{ formatDate(rate.asOf) }}</span>
            <span class="text-xs">{{ rate.source }}</span>
            <button class="text-gray-400 hover:text-red-600" @click="removeRate(rate.id)">✕</button>
          </span>
        </li>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue';
import type { ExchangeRate } from '../models/models';
import { fetchCurrentUser } from '../services/authService';
import {
  deleteExchangeRate,
  fetchExchangeRates,
  saveExchangeRate,
  updateBaseCurrency,
} from '../services/settingsApi';
import { CURRENCIES } from '../utils/currencies';
import { formatDate } from '../utils/labels';

const rates = ref<ExchangeRate[]>([]);
const baseCurrency = ref('EUR');
const savedBaseCurrency = ref('EUR');
const saving = ref(false);
const savedMessage = ref('');
const error = ref('');

const form = reactive({
  fromCurrency: 'EUR',
  toCurrency: 'HUF',
  rate: null as number | null,
  asOf: new Date().toISOString().split('T')[0],
});

async function load() {
  const [user, stored] = await Promise.all([fetchCurrentUser(), fetchExchangeRates()]);
  baseCurrency.value = user.baseCurrency;
  savedBaseCurrency.value = user.baseCurrency;
  rates.value = stored;
}

onMounted(load);

async function saveBaseCurrency() {
  saving.value = true;
  savedMessage.value = '';
  try {
    const user = await updateBaseCurrency(baseCurrency.value);
    savedBaseCurrency.value = user.baseCurrency;
    savedMessage.value = 'Saved';
  } finally {
    saving.value = false;
  }
}

async function addRate() {
  error.value = '';

  if (form.fromCurrency === form.toCurrency) {
    error.value = 'Pick two different currencies.';
    return;
  }
  if (!form.rate || form.rate <= 0) {
    error.value = 'A rate must be greater than zero.';
    return;
  }

  try {
    await saveExchangeRate(form.fromCurrency, form.toCurrency, form.rate, form.asOf);
    form.rate = null;
    rates.value = await fetchExchangeRates();
  } catch {
    error.value = 'Could not save that rate.';
  }
}

async function removeRate(id: number) {
  await deleteExchangeRate(id);
  rates.value = await fetchExchangeRates();
}
</script>
