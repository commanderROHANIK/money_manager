<template>
  <StatCard :label="t('loan.summary.label')">
    <template #value>
      {{ t('loan.summary.activePaidOff', { active: activeCount, paidOff: paidOffCount }) }}
    </template>
  </StatCard>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchLoans } from '../../../services/api';
import type { Loan } from '../../../models/models';
import StatCard from '../../ui/StatCard.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const loans = ref<Loan[]>([]);

onMounted(async () => {
  try {
    loans.value = await fetchLoans();
  } catch (error) {
    console.error('Failed to load loans:', error);
  }
});

const activeCount = computed(() => loans.value.filter(l => !l.isPaidOff).length);
const paidOffCount = computed(() => loans.value.filter(l => l.isPaidOff).length);
</script>
