<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-3 mb-4">
      <div>
        <h2 class="font-heading text-lg font-bold">{{ t('property.rentCollection.title') }}</h2>
        <p class="text-xs text-text-muted mt-1">
          {{ t('property.rentCollection.subtitle') }}
        </p>
      </div>

      <Badge :variant="schedule && schedule.arrears > 0 ? 'danger' : 'primary'">
        {{ arrearsLabel }}
      </Badge>
    </div>

    <p v-if="!schedule || schedule.periods.length === 0" class="text-sm text-text-muted">
      {{ t('property.rentCollection.empty') }}
    </p>

    <template v-else>
      <p v-if="props.error" class="text-sm text-danger-strong mb-3">{{ props.error }}</p>

      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="text-left text-xs text-text-muted border-b border-border">
              <th scope="col" class="py-2 pr-3 font-semibold">{{ t('property.rentCollection.month') }}</th>
              <th scope="col" class="py-2 pr-3 font-semibold">{{ t('property.rentCollection.due') }}</th>
              <th scope="col" class="py-2 pr-3 font-semibold text-right">{{ t('property.rentCollection.expected') }}</th>
              <th scope="col" class="py-2 pr-3 font-semibold text-right">{{ t('property.rentCollection.received') }}</th>
              <th scope="col" class="py-2 pr-3 font-semibold">{{ t('property.rentCollection.status') }}</th>
              <th scope="col" class="py-2 font-semibold"><span class="sr-only">{{ t('property.rentCollection.actions') }}</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="period in visiblePeriods" :key="period.period" class="border-b border-border/60">
              <td class="py-2 pr-3 font-medium whitespace-nowrap">{{ period.period }}</td>
              <td class="py-2 pr-3 text-text-muted whitespace-nowrap">{{ formatDate(period.dueDate) }}</td>

              <!-- A vacant month shows an em dash, not a zero: nothing was owed, which is not
                   the same as everything owed having been paid. -->
              <td class="py-2 pr-3 text-right tabular-nums">
                {{ period.expectedAmount === null ? '—' : formatMoney(period.expectedAmount, currencyCode) }}
              </td>
              <td class="py-2 pr-3 text-right tabular-nums">
                {{ formatMoney(period.receivedAmount, currencyCode) }}
              </td>

              <td class="py-2 pr-3">
                <Badge :variant="STATUS_VARIANTS[period.status]">
                  {{ RENT_STATUS_LABELS[period.status] }}
                </Badge>
                <span v-if="period.isOverdue" class="ml-2 text-xs text-danger-strong whitespace-nowrap">
                  {{
                    t('property.rentCollection.short', {
                      amount: formatMoney(period.shortfall, currencyCode),
                    })
                  }}
                </span>
              </td>

              <td class="py-2 text-right">
                <BaseButton
                  v-if="canRecord(period)"
                  size="sm"
                  variant="secondary"
                  :disabled="props.recording === period.period"
                  @click="record(period)"
                >
                  {{
                    props.recording === period.period
                      ? t('property.rentCollection.recording')
                      : t('property.rentCollection.markReceived')
                  }}
                </BaseButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <button
        v-if="schedule.periods.length > COLLAPSED_ROWS"
        class="mt-3 text-sm text-primary-strong hover:underline"
        @click="showAll = !showAll"
      >
        {{
          showAll
            ? t('property.rentCollection.showRecent')
            : t('property.rentCollection.showAll', { count: schedule.periods.length })
        }}
      </button>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { RentPeriodStatus, type RentPeriod, type RentSchedule } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { RENT_STATUS_LABELS, formatDate } from '../../../utils/labels';
import Badge from '../../ui/Badge.vue';
import BaseButton from '../../ui/BaseButton.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

/** Most recent months first is what a landlord actually looks at; the rest are one click away. */
const COLLAPSED_ROWS = 12;

const props = withDefaults(
  defineProps<{
    schedule: RentSchedule | null;
    currencyCode: string;
    /** Set by the page when a record call was refused, so the reason lands next to the table. */
    error?: string | null;
    /** The month currently being recorded, so its button can say so. */
    recording?: string | null;
  }>(),
  { error: null, recording: null }
);

const emit = defineEmits<{ (e: 'record', period: string): void }>();

const showAll = ref(false);

const STATUS_VARIANTS: Record<RentPeriodStatus, 'primary' | 'danger' | 'accent' | 'neutral'> = {
  [RentPeriodStatus.Paid]: 'primary',
  [RentPeriodStatus.Partial]: 'accent',
  [RentPeriodStatus.Unpaid]: 'danger',
  [RentPeriodStatus.Vacant]: 'neutral',
};

// Newest first: the months that need acting on are the recent ones.
const orderedPeriods = computed(() =>
  [...(props.schedule?.periods ?? [])].sort((a, b) => b.period.localeCompare(a.period))
);

const visiblePeriods = computed(() =>
  showAll.value ? orderedPeriods.value : orderedPeriods.value.slice(0, COLLAPSED_ROWS)
);

const arrearsLabel = computed(() => {
  if (!props.schedule || props.schedule.overduePeriodCount === 0)
    return t('property.rentCollection.upToDate');

  return t(
    'property.rentCollection.behind',
    {
      amount: formatMoney(props.schedule.arrears, props.currencyCode),
      count: props.schedule.overduePeriodCount,
    },
    props.schedule.overduePeriodCount
  );
});

// Nothing was owed for a vacant month, and a settled one needs no button. Recording against a
// partly-paid month is refused by the server rather than doubling the entry, so the button is
// not offered for it either — the ledger is where a correction belongs.
function canRecord(period: RentPeriod): boolean {
  return period.status === RentPeriodStatus.Unpaid;
}

// The page owns every call, the same way it does for the ledger and tenancy widgets, so this
// stays presentational and cannot disagree with the server about what happened.
function record(period: RentPeriod) {
  emit('record', period.period);
}
</script>
