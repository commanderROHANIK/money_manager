<template>
  <BaseCard :title="t('settings.ratesTitle')">
    <p class="mb-4 text-sm text-text-muted">
      {{ automatic ? t('settings.ratesIntroAutomatic') : t('settings.ratesIntroManual') }}
    </p>

    <form class="flex flex-wrap items-end gap-3" @submit.prevent="save">
      <BaseSelect v-model="from" :label="t('settings.oneUnitOf')" class="w-32">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <BaseSelect v-model="to" :label="t('settings.isWorthIn')" class="w-32">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <template v-if="!useLive">
        <BaseInput v-model.number="rate" :label="t('settings.amount')" type="number" step="any" min="0" class="w-40" />

        <BaseInput v-model="asOf" :label="t('settings.asOf')" type="date" class="w-44" />
      </template>

      <BaseButton type="submit" :disabled="saving">{{
        useLive ? t('settings.useLiveRate') : t('settings.saveRate')
      }}</BaseButton>
    </form>

    <!--
      The control this widget was missing. Fetching only ever filled in the pairs nobody had
      typed a rate for, which is correct and completely invisible: a user who had entered a rate
      once had no way to say "stop using mine, use whatever today's is" short of guessing that
      deleting the row would do it.
    -->
    <label v-if="automatic" class="mt-3 flex items-start gap-2.5 text-sm">
      <input v-model="useLive" type="checkbox" class="mt-0.5" />
      <span>
        <span class="font-semibold">{{ t('settings.useLiveRateLabel') }}</span>
        <span class="block text-text-muted">{{ t('settings.useLiveRateHint') }}</span>
      </span>
    </label>

    <p v-if="error" class="mt-3 text-sm text-danger">{{ error }}</p>

    <EmptyState
      v-if="rates.length === 0"
      class="mt-4"
      :title="t('settings.noRates')"
      :description="automatic ? t('settings.noRatesHintAutomatic') : t('settings.noRatesHint')"
    />

    <ul v-else class="mt-4 divide-y divide-border">
      <li v-for="entry in rates" :key="entry.id" class="flex items-center justify-between gap-3 py-2.5 text-sm">
        <span class="tabular-nums">
          1 {{ entry.baseCurrency }} = {{ entry.rate }} {{ entry.quoteCurrency }}
        </span>
        <span class="flex items-center gap-3">
          <span class="text-xs text-text-muted">{{ provenance(entry) }}</span>

          <!--
            Offered per row, because the choice is genuinely per pair: you might want the ECB's
            EUR/HUF and your bank's actual GBP/HUF from the day you moved the money. A single
            account-wide switch would make you give up the second to get the first.
          -->
          <BaseButton
            v-if="automatic && entry.source === ExchangeRateSource.Manual"
            variant="secondary"
            size="sm"
            :disabled="switching === entry.id"
            @click="useLiveFor(entry)"
          >{{ switching === entry.id ? t('settings.refreshing') : t('settings.useLiveRate') }}</BaseButton>

          <BaseButton variant="danger" size="sm" @click="remove(entry)">{{
            t('settings.remove')
          }}</BaseButton>
        </span>
      </li>
    </ul>

    <!--
      Shown only where fetching actually happens. Naming the source is the point of the whole
      change: a converted total that cannot say where its rate came from is a spreadsheet with
      better fonts.
    -->
    <div v-if="automatic" class="mt-4 border-t border-border pt-3">
      <p class="text-xs text-text-muted">{{ t('settings.ratesProvider') }}</p>
      <div class="mt-2 flex items-center gap-3">
        <BaseButton variant="secondary" size="sm" :disabled="refreshing" @click="refresh">
          {{ refreshing ? t('settings.refreshing') : t('settings.refreshRates') }}
        </BaseButton>
        <span v-if="refreshError" class="text-xs text-danger">{{ refreshError }}</span>
      </div>
    </div>
  </BaseCard>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from 'vue';
import type { ExchangeRate } from '../../../models/models';
import { ExchangeRateSource } from '../../../models/models';
import {
  deleteExchangeRate,
  fetchExchangeRates,
  refreshExchangeRates,
  upsertExchangeRate,
} from '../../../services/exchangeRateApi';
import { featureFlags } from '../../../services/features';
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
const refreshing = ref(false);
const useLive = ref(false);
const switching = ref<number | null>(null);
const error = ref('');
const refreshError = ref('');

/**
 * Whether this deployment fetches at all. Read from the server rather than assumed, because the
 * two states look identical from here — a table of rows says nothing about where the next one
 * would come from — and describing fetching that is switched off would be worse than saying
 * nothing.
 */
const automatic = computed(() => featureFlags.value.automaticExchangeRates);

onMounted(load);

async function load() {
  try {
    rates.value = await fetchExchangeRates();
  } catch (err) {
    console.error('Failed to load exchange rates:', err);
  }
}

/** Where one row came from, and when. Per row, because a table can hold both kinds at once. */
function provenance(entry: ExchangeRate): string {
  const date = formatDate(entry.asOf);

  return entry.source === ExchangeRateSource.Ecb
    ? t('settings.sourceEcb', { date })
    : t('settings.sourceManual', { date });
}

async function save() {
  error.value = '';

  if (from.value === to.value) {
    error.value = t('settings.sameCurrency');
    return;
  }

  if (useLive.value) {
    await switchToLive(from.value, to.value);
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

/**
 * Asks the server to fetch now instead of waiting out its cache window.
 *
 * <p>Rows the user entered are left alone by the server, so this cannot quietly undo an override —
 * which is what makes the button safe to offer next to a table the user also edits by hand.</p>
 */
async function refresh() {
  refreshError.value = '';
  refreshing.value = true;

  try {
    rates.value = await refreshExchangeRates();
    emit('changed');
  } catch (err) {
    console.error('Failed to refresh exchange rates:', err);
    refreshError.value = t('settings.refreshFailed');
  } finally {
    refreshing.value = false;
  }
}

/** The row-level version of the checkbox: stop using this hand-entered rate, use the live one. */
async function useLiveFor(entry: ExchangeRate) {
  switching.value = entry.id;

  try {
    await switchToLive(entry.baseCurrency, entry.quoteCurrency);
  } finally {
    switching.value = null;
  }
}

/**
 * Hands a pair back to the provider.
 *
 * <p>Implemented as "remove what you entered", because that is literally what automatic means
 * here — a pair nobody has spoken for. The server forgets its fetch window on a delete, so the
 * reload below fetches rather than answering from a cache that already decided this pair was
 * covered. Without that the row would vanish and nothing would take its place until the window
 * expired, which is exactly what "it still doesn't work" looks like from the outside.</p>
 *
 * <p>The user's figure is gone afterwards. That is what they asked for, but it is also why this
 * says so when no live rate arrives, rather than leaving a silently emptier table.</p>
 */
async function switchToLive(baseCurrency: string, quoteCurrency: string) {
  error.value = '';
  saving.value = true;

  try {
    await deleteExchangeRate(baseCurrency, quoteCurrency);
    await load();
    emit('changed');

    const arrived = rates.value.some(
      (r) =>
        (r.baseCurrency === baseCurrency && r.quoteCurrency === quoteCurrency) ||
        (r.baseCurrency === quoteCurrency && r.quoteCurrency === baseCurrency)
    );

    if (!arrived) error.value = t('settings.noLiveRateYet');
  } catch (err) {
    console.error('Failed to switch to the live rate:', err);
    error.value = t('settings.useLiveRateFailed');
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
