<template>
    <div class="bg-white p-6 rounded-2xl shadow-md">
      <h2 class="text-lg font-semibold mb-2">Rental Properties</h2>
      <p class="text-sm">{{ rentedCount }} Rented | {{ vacantCount }} Vacant</p>
    </div>
  </template>
  
  <script setup lang="ts">
  import { ref, onMounted, computed } from 'vue';
  import { fetchRentalProperties } from '../../services/api';
  import type { RentalProperty } from '../../models/models';
  
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
  