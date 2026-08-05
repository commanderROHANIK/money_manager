<template>
  <StatCard label="Loans">
    <template #value>{{ activeCount }} Active | {{ paidOffCount }} Paid Off</template>
  </StatCard>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchLoans } from '../../../services/api';
import type { Loan } from '../../../models/models';
import StatCard from '../../ui/StatCard.vue';

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
