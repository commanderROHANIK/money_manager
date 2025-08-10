<template>
    <div class="bg-white p-6 rounded-2xl shadow-md">
      <h2 class="text-lg font-semibold mb-2">Loans</h2>
      <p class="text-sm">
        {{ activeCount }} Active | {{ paidOffCount }} Paid Off
      </p>
    </div>
  </template>
  
  <script setup lang="ts">
  import { ref, onMounted, computed } from 'vue';
  import { fetchLoans } from '../../../services/api';
  import type { Loan } from '../../../models/models';
  
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
  