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
import type { PortfolioAnalytics } from '../../../models/models';
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
 * Shown whenever a rate was applied. Not decoration: a converted total is an estimate built from
 * a number the user typed in themselves, and it has to read differently from a figure that came
 * straight out of the ledger.
 */
const conversionNote = computed(() => {
  const p = props.portfolio;
  if (!p || !p.converted || p.appliedRates.length === 0) return '';

  const rates = p.appliedRates
    .map(
      (r) =>
        `1 ${r.from} = ${r.rate} ${r.to}` +
        (r.asOf ? ` ${t('property.portfolio.rateOn', { date: formatDate(r.asOf) })}` : '')
    )
    .join('; ');

  return t('property.portfolio.convertedNote', { currency: p.currency, rates });
});

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
