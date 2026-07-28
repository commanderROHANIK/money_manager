<template>
  <div class="p-4 space-y-4">
    <p v-if="loading" class="text-gray-500">Loading…</p>
    <p v-else-if="error" class="text-red-600">{{ error }}</p>

    <template v-else-if="property && metrics">
      <!-- Header -->
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <router-link to="/properties" class="text-sm text-blue-600 hover:underline">
            ← All properties
          </router-link>
          <h1 class="text-2xl font-bold mt-1">{{ property.propertyName }}</h1>
          <p class="text-sm text-gray-500">
            {{ property.address }}<span v-if="property.city">, {{ property.city }}</span>
            · {{ PROPERTY_TYPE_LABELS[property.propertyType] }}
            <span v-if="property.sizeSqm"> · {{ property.sizeSqm }} m²</span>
            <span v-if="property.bedrooms"> · {{ property.bedrooms }} bed</span>
          </p>
        </div>
        <span
          class="text-sm font-medium px-3 py-1 rounded-full"
          :class="property.isRented ? 'bg-green-100 text-green-800' : 'bg-yellow-100 text-yellow-800'"
        >
          {{ property.isRented ? `Let to ${property.tenantName}` : 'Vacant' }}
        </span>
      </div>

      <div class="bg-white dark:bg-gray-800 p-4 rounded-2xl shadow-md">
        <PropertyMetricsWidget :metrics="metrics" />
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="bg-white dark:bg-gray-800 p-4 rounded-2xl shadow-md">
          <RentVsMarketWidget :metrics="metrics" @add-estimate="onAddEstimate" />
        </div>
        <div class="xl:col-span-2 bg-white dark:bg-gray-800 p-4 rounded-2xl shadow-md">
          <RentOverTimeChartWidget :history="rentHistory" :currency-code="property.currencyCode" />
        </div>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="bg-white dark:bg-gray-800 p-4 rounded-2xl shadow-md">
          <TenancyWidget :leases="leases" @create="onCreateLease" />
        </div>
        <div class="xl:col-span-2 bg-white dark:bg-gray-800 p-4 rounded-2xl shadow-md">
          <TransactionLedgerWidget
            :transactions="transactions"
            @create="onCreateTransaction"
            @delete="onDeleteTransaction"
          />
        </div>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="bg-white dark:bg-gray-800 p-4 rounded-2xl shadow-md">
          <ValuationWidget
            :valuations="valuations"
            :currency-code="property.currencyCode"
            @create="onCreateValuation"
          />
        </div>
        <div class="xl:col-span-2 bg-white dark:bg-gray-800 p-4 rounded-2xl shadow-md">
          <PropertyTimelineWidget :events="events" />
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import type {
  Lease,
  PropertyEvent,
  PropertyMetrics,
  PropertyTransaction,
  PropertyValuation,
  RentPricePoint,
  RentalProperty,
} from '../models/models';
import {
  addMarketEstimate,
  createLease,
  createTransaction,
  createValuation,
  deleteTransaction,
  fetchLeases,
  fetchProperty,
  fetchPropertyEvents,
  fetchPropertyMetrics,
  fetchRentHistory,
  fetchTransactions,
  fetchValuations,
} from '../services/propertyApi';
import type { LeaseRequest } from '../services/propertyApi';
import { PROPERTY_TYPE_LABELS } from '../utils/labels';

import PropertyMetricsWidget from './Widgets/Properties/PropertyMetricsWidget.vue';
import RentVsMarketWidget from './Widgets/Properties/RentVsMarketWidget.vue';
import RentOverTimeChartWidget from './Widgets/Properties/RentOverTimeChartWidget.vue';
import TenancyWidget from './Widgets/Properties/TenancyWidget.vue';
import TransactionLedgerWidget from './Widgets/Properties/TransactionLedgerWidget.vue';
import PropertyTimelineWidget from './Widgets/Properties/PropertyTimelineWidget.vue';
import ValuationWidget from './Widgets/Properties/ValuationWidget.vue';

const route = useRoute();
const propertyId = Number(route.params.id);

const loading = ref(true);
const error = ref('');

const property = ref<RentalProperty | null>(null);
const metrics = ref<PropertyMetrics | null>(null);
const transactions = ref<PropertyTransaction[]>([]);
const leases = ref<Lease[]>([]);
const rentHistory = ref<RentPricePoint[]>([]);
const valuations = ref<PropertyValuation[]>([]);
const events = ref<PropertyEvent[]>([]);

// The page owns every fetch and passes data down, keeping the widgets presentational.
async function load() {
  loading.value = true;
  error.value = '';

  try {
    [
      property.value,
      metrics.value,
      transactions.value,
      leases.value,
      rentHistory.value,
      valuations.value,
      events.value,
    ] = await Promise.all([
      fetchProperty(propertyId),
      fetchPropertyMetrics(propertyId),
      fetchTransactions(propertyId),
      fetchLeases(propertyId),
      fetchRentHistory(propertyId),
      fetchValuations(propertyId),
      fetchPropertyEvents(propertyId),
    ]);
  } catch {
    error.value = 'Could not load this property.';
  } finally {
    loading.value = false;
  }
}

onMounted(load);

// Anything that changes the ledger changes the metrics, so the page reloads rather than
// trying to recompute the figures client-side and risk disagreeing with the server.
async function onCreateTransaction(payload: {
  date: string;
  amount: number;
  category: number;
  description: string;
}) {
  await createTransaction(propertyId, payload);
  await load();
}

async function onDeleteTransaction(id: number) {
  await deleteTransaction(propertyId, id);
  await load();
}

async function onCreateLease(payload: LeaseRequest) {
  await createLease(propertyId, payload);
  await load();
}

async function onAddEstimate(amount: number) {
  await addMarketEstimate(propertyId, amount);
  await load();
}

async function onCreateValuation(payload: { valuedOn: string; value: number }) {
  await createValuation(propertyId, payload.valuedOn, payload.value);
  await load();
}
</script>
