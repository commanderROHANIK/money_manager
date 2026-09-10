<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-1">{{ t('property.underpriced.title') }}</h2>
    <p class="text-xs text-text-muted mb-3">{{ t('property.underpriced.subtitle') }}</p>

    <p v-if="underpriced.length === 0" class="text-sm text-text-muted">
      {{ t('property.underpriced.empty') }}
    </p>

    <div v-else>
      <p v-if="missingRateMessage" class="text-sm text-accent-strong mb-3">
        {{ missingRateMessage }}
        <router-link to="/settings" class="font-semibold underline">{{
          t('property.portfolio.addRateLink')
        }}</router-link>
        {{ t('property.portfolio.addRateSuffix') }}
      </p>

      <p class="font-heading text-3xl font-extrabold tabular-nums text-accent-strong mb-1">
        {{ totalUpliftLabel }}<span class="text-base font-normal text-text-muted"> {{ t('property.underpriced.perYear') }}</span>
      </p>
      <p v-if="conversionNote" class="text-xs text-text-muted mb-3">{{ conversionNote }}</p>

      <ul>
        <ListRow v-for="item in underpriced" :key="item.propertyId">
          <template #title>
            <router-link
              :to="`/properties/${item.propertyId}`"
              class="text-primary-strong hover:underline truncate"
            >
              {{ item.propertyName }}
            </router-link>
          </template>
          <template #trailing>
            <span class="text-sm whitespace-nowrap">
              <span class="text-accent-strong font-medium">{{ formatPercent(item.rentGapPercent) }}</span>
              <span class="text-text-muted">
                · {{ formatMoney(item.annualRentUplift, item.currencyCode) }}/yr
              </span>
            </span>
          </template>
        </ListRow>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { PortfolioAnalytics } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatPercent } from '../../../utils/labels';
import { useRateDisclosure } from '../../../composables/useRateDisclosure';
import ListRow from '../../ui/ListRow.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps<{ portfolio: PortfolioAnalytics | null }>();

const underpriced = computed(() =>
  (props.portfolio?.properties ?? [])
    .filter((m) => (m.rentGapPercent ?? 0) > 0 && (m.annualRentUplift ?? 0) > 0)
    .sort((a, b) => (b.annualRentUplift ?? 0) - (a.annualRentUplift ?? 0))
);

// The portfolio's own converted total (CurrencyRollup.Sum on the backend, summing only the
// underpriced properties — see PortfolioAnalyticsDto.From) rather than a client-side sum: the
// same "never sum across currencies without a rate" rule the portfolio summary already follows.
const totalUpliftLabel = computed(() => {
  const total = props.portfolio?.totalAnnualRentUplift;
  if (total === null || total === undefined) return '—';
  return formatMoney(total, props.portfolio?.currency ?? 'EUR');
});

const { conversionNote, missingRateMessage } = useRateDisclosure(computed(() => props.portfolio));
</script>
