import { computed } from 'vue';
import type { ComputedRef } from 'vue';
import { useI18n } from 'vue-i18n';
import type { AppliedRate, CurrencyPair } from '../models/models';
import { ExchangeRateSource } from '../models/models';
import { formatDate } from '../utils/labels';

/** The shape any converted-total DTO carries — portfolio rollup, bank balance, stock value. */
export interface RateDisclosureSource {
  currency: string | null;
  converted: boolean;
  appliedRates: AppliedRate[];
  missingRates: CurrencyPair[];
}

/**
 * The two lines every converted total needs next to it: what rate it was built from, and what
 * rate is missing when it couldn't be built at all. Pulled out of `PortfolioSummaryWidget` once
 * a second and third widget needed the identical disclosure — reused rather than reimplemented,
 * so "a converted total names the rate it used" stays one behavior instead of drifting into
 * several worded slightly differently.
 */
export function useRateDisclosure(source: ComputedRef<RateDisclosureSource | null | undefined>): {
  conversionNote: ComputedRef<string>;
  missingRateMessage: ComputedRef<string>;
} {
  const { t } = useI18n();

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

  const conversionNote = computed(() => {
    const s = source.value;
    if (!s || !s.converted || s.appliedRates.length === 0) return '';

    const rates = s.appliedRates.map(describe).join('; ');
    return t('property.portfolio.convertedNote', { currency: s.currency, rates });
  });

  const missingRateMessage = computed(() => {
    const missing = source.value?.missingRates ?? [];
    if (missing.length === 0) return '';

    const pairs = missing.map((pair) => `${pair.from} → ${pair.to}`).join(', ');
    return t('property.portfolio.missingRate', { pairs });
  });

  return { conversionNote, missingRateMessage };
}
