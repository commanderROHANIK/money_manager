<template>
  <div class="p-4 space-y-4">
    <LoadingSkeleton v-if="loading" />
    <ErrorState
      v-else-if="error"
      :title="t('property.detail.loadFailed')"
      :description="error"
    />

    <template v-else-if="property && metrics">
      <!-- Header -->
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <router-link to="/properties" class="text-sm text-primary-strong hover:underline">
            ← All properties
          </router-link>
          <h1 class="font-heading text-2xl font-bold mt-1">{{ property.propertyName }}</h1>
          <p class="text-sm text-text-muted">
            {{ property.address }}<span v-if="property.city">, {{ property.city }}</span>
            · {{ PROPERTY_TYPE_LABELS[property.propertyType] }}
            <span v-if="property.sizeSqm"> · {{ property.sizeSqm }} m²</span>
            <span v-if="property.bedrooms"> · {{ property.bedrooms }} bed</span>
          </p>
        </div>
        <Badge :variant="property.isRented ? 'primary' : 'neutral'">
          {{
            property.isRented
              ? t('property.letTo', { tenant: property.tenantName })
              : t('property.vacant')
          }}
        </Badge>
      </div>

      <BaseCard>
        <PropertyMetricsWidget :metrics="metrics" />
      </BaseCard>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <BaseCard>
          <RentVsMarketWidget :metrics="metrics" @add-estimate="onAddEstimate" />
        </BaseCard>
        <BaseCard class="xl:col-span-2">
          <RentOverTimeChartWidget :history="rentHistory" :currency-code="property.currencyCode" />
        </BaseCard>
      </div>

      <BaseCard>
        <RentCollectionWidget
          :schedule="rentSchedule"
          :currency-code="property.currencyCode"
          :error="rentError"
          :recording="recordingPeriod"
          @record="onRecordRent"
        />
      </BaseCard>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <BaseCard>
          <TenancyWidget :leases="leases" @create="onCreateLease" />
        </BaseCard>
        <BaseCard class="xl:col-span-2">
          <TransactionLedgerWidget
            :transactions="transactions"
            @create="onCreateTransaction"
            @delete="onDeleteTransaction"
          />
        </BaseCard>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <BaseCard>
          <ValuationWidget
            :valuations="valuations"
            :currency-code="property.currencyCode"
            @create="onCreateValuation"
          />
        </BaseCard>
        <BaseCard class="xl:col-span-2">
          <PropertyTimelineWidget :events="events" />
        </BaseCard>
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
  RentSchedule,
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
  fetchRentSchedule,
  fetchTransactions,
  fetchValuations,
  recordRentForPeriod,
} from '../services/propertyApi';
import type { LeaseRequest } from '../services/propertyApi';
import { PROPERTY_TYPE_LABELS } from '../utils/labels';

import PropertyMetricsWidget from './Widgets/Properties/PropertyMetricsWidget.vue';
import RentVsMarketWidget from './Widgets/Properties/RentVsMarketWidget.vue';
import RentOverTimeChartWidget from './Widgets/Properties/RentOverTimeChartWidget.vue';
import RentCollectionWidget from './Widgets/Properties/RentCollectionWidget.vue';
import TenancyWidget from './Widgets/Properties/TenancyWidget.vue';
import TransactionLedgerWidget from './Widgets/Properties/TransactionLedgerWidget.vue';
import PropertyTimelineWidget from './Widgets/Properties/PropertyTimelineWidget.vue';
import ValuationWidget from './Widgets/Properties/ValuationWidget.vue';
import BaseCard from './ui/BaseCard.vue';
import Badge from './ui/Badge.vue';
import LoadingSkeleton from './ui/LoadingSkeleton.vue';
import ErrorState from './ui/ErrorState.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

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
const rentSchedule = ref<RentSchedule | null>(null);

const rentError = ref('');
const recordingPeriod = ref<string | null>(null);

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
      rentSchedule.value,
    ] = await Promise.all([
      fetchProperty(propertyId),
      fetchPropertyMetrics(propertyId),
      fetchTransactions(propertyId),
      fetchLeases(propertyId),
      fetchRentHistory(propertyId),
      fetchValuations(propertyId),
      fetchPropertyEvents(propertyId),
      fetchRentSchedule(propertyId),
    ]);
  } catch {
    error.value = t('property.detail.loadFailed');
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

/**
 * The server refuses a second payment for a month that already has one, and refuses a month
 * where no tenancy was running. Both come back as a message worth showing rather than swallowing
 * — a button that silently does nothing is worse than one that says why.
 */
async function onRecordRent(period: string) {
  recordingPeriod.value = period;
  rentError.value = '';

  try {
    await recordRentForPeriod(propertyId, period);
    await load();
  } catch (e) {
    rentError.value = messageFrom(e) ?? t('property.recordRentFailed', { period });
  } finally {
    recordingPeriod.value = null;
  }
}

/** Pulls the server's own explanation out of an axios error, when it sent one. */
function messageFrom(error: unknown): string | null {
  const body = (error as { response?: { data?: { message?: unknown } } })?.response?.data;
  return typeof body?.message === 'string' ? body.message : null;
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
