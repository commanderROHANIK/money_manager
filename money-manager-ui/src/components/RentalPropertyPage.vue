<template>
  <div class="p-4 grid grid-cols-1 xl:grid-cols-3 gap-4">
    <!-- Top Row Widgets -->
    <div class="col-span-1 bg-white p-4 rounded-2xl shadow-md">
      <TotalRentWidget :properties="properties" />
    </div>

    <div class="col-span-1 bg-white p-4 rounded-2xl shadow-md">
      <RentedVsVacantPieWidget :properties="properties" />
    </div>

    <div class="col-span-1 bg-white p-4 rounded-2xl shadow-md">
      <UpcomingRentDueWidget :properties="properties" />
    </div>

    <!-- Properties List -->
    <div class="col-span-1 xl:col-span-3 bg-white p-4 rounded-2xl shadow-md">
      <PropertyListWidget :properties="properties" @delete-property="_deleteProperty" />
    </div>

    <!-- Bottom Row -->
    <div class="col-span-1 bg-white p-4 rounded-2xl shadow-md">
      <MostExpensivePropertyWidget :properties="properties" />
    </div>
    <div class="col-span-1 xl:col-span-2 bg-white p-4 rounded-2xl shadow-md">
      <RentByMonthChartWidget :properties="properties" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { fetchRentalProperties, deleteRentalProperty } from '../services/api';
import type { RentalProperty } from '../models/models';

// Widgets
import TotalRentWidget from '../components/Widgets/TotalRentWidget.vue';
import RentedVsVacantPieWidget from '../components/Widgets/RentedVsVacantPieWidget.vue';
import UpcomingRentDueWidget from '../components/Widgets/UpcomingRentDueWidget.vue';
import PropertyListWidget from '../components/Widgets/PropertyListWidget.vue';
import MostExpensivePropertyWidget from '../components/Widgets/MostExpensivePropertyWidget.vue';
import RentByMonthChartWidget from '../components/Widgets/RentByMonthChartWidget.vue';

const properties = ref<RentalProperty[]>([]);

onMounted(async () => {
  properties.value = await fetchRentalProperties();
});

async function _deleteProperty(id: number) {
  await deleteRentalProperty(id);
  properties.value = properties.value.filter(p => p.id !== id);
}
</script>
