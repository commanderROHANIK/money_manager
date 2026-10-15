<template>
  <StatCard
    :label="t('loan.total.label')"
    :value="formattedTotal"
    :delta="note"
    :delta-positive="!summary?.missingRates.length"
  />
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchLoansTotalAmount } from '../../../services/api';
import type { LoanAmountSummary } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { useRateDisclosure } from '../../../composables/useRateDisclosure';
import StatCard from '../../ui/StatCard.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const summary = ref<LoanAmountSummary | null>(null);

onMounted(async () => {
  try {
    summary.value = await fetchLoansTotalAmount();
  } catch (err) {
    console.error('Failed to fetch loan total:', err);
  }
});

/**
 * Rendered in the currency the server says the figure is in, rather than the hardcoded HUF this
 * used to force onto every total regardless of what currency the loans were actually in. A null
 * total means loans span currencies with no rate to combine them — unknown, not zero.
 */
const formattedTotal = computed(() => {
  const s = summary.value;
  if (!s) return '';
  if (s.totalAmount === null) return '—';
  return formatMoney(s.totalAmount, s.currency);
});

const { conversionNote, missingRateMessage } = useRateDisclosure(computed(() => summary.value));

const note = computed(() => missingRateMessage.value || conversionNote.value || undefined);
</script>
