<template>
    <StatCard label="Rental Properties">
      <template #value>{{ rentedCount }} Rented | {{ vacantCount }} Vacant</template>
    </StatCard>
  </template>

  <script setup lang="ts">
  import { ref, onMounted, computed } from 'vue';
  import { fetchRentalProperties } from '../../../services/api';
  import type { RentalProperty } from '../../../models/models';
  import StatCard from '../../ui/StatCard.vue';

  const properties = ref<RentalProperty[]>([]);
  
  onMounted(async () => {
    try {
      properties.value = await fetchRentalProperties();
    } catch (error) {
      console.error('Failed to load rental properties:', error);
    }
  });
  
  const rentedCount = computed(() =>
    properties.value.filter(p => p.isRented).length
  );
  const vacantCount = computed(() =>
    properties.value.filter(p => !p.isRented).length
  );
  </script>
  