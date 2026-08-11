<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-3">{{ t('property.portfolio.title') }}</h2>

    <p v-if="!portfolio || portfolio.propertyCount === 0" class="text-sm text-text-muted">
      {{ t('property.portfolio.empty') }}
    </p>

    <template v-else>
      <p v-if="missingRateMessage" class="text-sm text-accent-strong mb-3">
        {{ missingRateMessage }}
        <router-link to="/settings" class="font-semibold underline">{{
          t('property.portfolio.addRateLink')
        }}</router-link>
        {{ t('property.portfolio.addRateSuffix') }}
      </p>

      <div class="grid grid-cols-2 md:grid-cols-5 gap-3">
        <div v-for="tile in tiles" :key="tile.label" class="p-3 rounded-lg bg-surface-2">
          <p class="text-xs text-text-muted">{{ tile.label }}</p>
          <p class="text-lg font-bold tabular-nums" :class="tile.tone">{{ tile.value }}</p>
        </div>
      </div>

      <p v-if="conversionNote" class="text-xs text-text-muted mt-3">{{ conversionNote }}</p>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { AppliedRate, PortfolioAnalytics } from '../../../models/models';
import { ExchangeRateSource } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatPercent, formatDate } from '../../../utils/labels';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps<{ portfolio: PortfolioAnalytics | null }>();

function money(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  // The portfolio's own currency, never a hardcoded one: after conversion these figures are in
  // the base currency, and labelling them with anything else would misstate them by a factor.
  return formatMoney(value, props.portfolio?.currency ?? 'EUR');
}

/**
 * Shown whenever a rate was applied. Not decoration: a converted total is an estimate, and it has
 * to read differently from a figure that came straight out of the ledger.
 *
 * Each rate names where it came from, because the two provenances carry different weight. A rate
 * the user entered is an assertion they can defend; an ECB reference rate is a published daily
 * figure that no bank will match exactly. Saying "the rates you entered" over a fetched number —
 * which is what this line used to do — is the confident wrong statement in miniature.
 *
 * The source travels on the applied rate rather than being looked up again here, so the figure
 * disclosed is always the figure the total was built from, even if the table has since moved on.
 */
const conversionNote = computed(() => {
  const p = props.portfolio;
  if (!p || !p.converted || p.appliedRates.length === 0) return '';

  const rates = p.appliedRates.map((r) => describe(r)).join('; ');

  return t('property.portfolio.convertedNote', { currency: p.currency, rates });
});

function describe(rate: AppliedRate): string {
  const parts = { from: rate.from, to: rate.to, rate: String(rate.rate) };

  // Both null together, and only for a conversion no stored row backs. There is nothing to
  // attribute and no date to give, so the line says the arithmetic and stops.
  if (rate.asOf === null || rate.source === null) return t('property.portfolio.ratePlain', parts);

  const key =
    rate.source === ExchangeRateSource.Ecb
      ? 'property.portfolio.rateEcb'
      : 'property.portfolio.rateManual';

  return t(key, { ...parts, date: formatDate(rate.asOf) });
}

const missingRateMessage = computed(() => {
  const missing = props.portfolio?.missingRates ?? [];
  if (missing.length === 0) return '';

  const pairs = missing.map((pair) => `${pair.from} → ${pair.to}`).join(', ');
  return t('property.portfolio.missingRate', { pairs });
});

const tiles = computed(() => {
  const p = props.portfolio;
  if (!p) return [];

  return [
    { label: t('property.portfolio.tileProperties'), value: String(p.propertyCount), tone: '' },
    { label: t('property.portfolio.tileInvested'), value: money(p.totalInvested), tone: '' },
    { label: t('property.portfolio.tileEquity'), value: money(p.totalEquity), tone: '' },
    {
      label: t('property.portfolio.tileCashFlow'),
      value: money(p.totalMonthlyCashFlow),
      tone: (p.totalMonthlyCashFlow ?? 0) >= 0 ? 'text-primary-strong' : 'text-danger',
    },
    {
      label: t('property.portfolio.tileRoi'),
      value: formatPercent(p.portfolioRoi),
      tone: (p.portfolioRoi ?? 0) >= 0 ? 'text-primary-strong' : 'text-danger',
    },
  ];
});
</script>
