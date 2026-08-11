<template>
    <StatCard :label="t('property.summary.label')" :delta="arrearsDelta" :delta-positive="arrears.length === 0">
      <template #value>
        {{ t('property.summary.rentedVacant', { rented: rentedCount, vacant: vacantCount }) }}
      </template>
    </StatCard>
  </template>

  <script setup lang="ts">
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
  import { ref, onMounted, computed } from 'vue';
  import { fetchRentalProperties } from '../../../services/api';
  import { fetchArrears } from '../../../services/propertyApi';
  import type { PropertyArrears, RentalProperty } from '../../../models/models';
  import StatCard from '../../ui/StatCard.vue';

  const properties = ref<RentalProperty[]>([]);
  const arrears = ref<PropertyArrears[]>([]);

  onMounted(async () => {
    try {
      [properties.value, arrears.value] = await Promise.all([
        fetchRentalProperties(),
        fetchArrears(),
      ]);
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

  // Undefined rather than "0 behind on rent": StatCard hides the line entirely when there is
  // nothing to say, and a zero here would be one more number to read past every morning.
  const arrearsDelta = computed(() => {
    if (arrears.value.length === 0) return undefined;

    return t('property.summary.behindOnRent', arrears.value.length);
  });
  </script>
