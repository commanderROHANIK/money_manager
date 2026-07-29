<template>
  <div class="p-4 grid grid-cols-1 xl:grid-cols-3 gap-4">
    <!-- Portfolio headline -->
    <div class="col-span-1 xl:col-span-3 bg-white p-4 rounded-2xl shadow-md">
      <PortfolioSummaryWidget :portfolio="portfolio" />
    </div>

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

    <!-- The commercial hook: where rent is trailing the market -->
    <div class="col-span-1 xl:col-span-2 bg-white p-4 rounded-2xl shadow-md">
      <UnderpricedPropertiesWidget :metrics="portfolio?.properties ?? []" />
    </div>

    <div class="col-span-1 bg-white p-4 rounded-2xl shadow-md">
      <MostExpensivePropertyWidget :properties="properties" />
    </div>

    <!-- Properties List -->
    <div class="col-span-1 xl:col-span-3 bg-white p-4 rounded-2xl shadow-md">
      <PropertyListWidget :properties="properties" @delete-property="_deleteProperty" />
    </div>

    <div class="col-span-1 xl:col-span-3 bg-white p-4 rounded-2xl shadow-md">
      <AddPropertyWidget @create="_addProperty" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { fetchRentalProperties, deleteRentalProperty } from '../services/api';
import { createProperty, fetchPortfolioAnalytics } from '../services/propertyApi';
import type { RentalPropertyRequest } from '../services/propertyApi';
import type { PortfolioAnalytics, RentalProperty } from '../models/models';

// Widgets
import TotalRentWidget from '../components/Widgets/Properties/TotalRentWidget.vue';
import RentedVsVacantPieWidget from '../components/Widgets/Properties/RentedVsVacantPieWidget.vue';
import UpcomingRentDueWidget from '../components/Widgets/Properties/UpcomingRentDueWidget.vue';
import PropertyListWidget from '../components/Widgets/Properties/PropertyListWidget.vue';
import MostExpensivePropertyWidget from '../components/Widgets/Properties/MostExpensivePropertyWidget.vue';
import UnderpricedPropertiesWidget from '../components/Widgets/Properties/UnderpricedPropertiesWidget.vue';
import PortfolioSummaryWidget from '../components/Widgets/Properties/PortfolioSummaryWidget.vue';
import AddPropertyWidget from '../components/Widgets/Properties/AddPropertyWidget.vue';

const properties = ref<RentalProperty[]>([]);
const portfolio = ref<PortfolioAnalytics | null>(null);

async function load() {
  [properties.value, portfolio.value] = await Promise.all([
    fetchRentalProperties(),
    fetchPortfolioAnalytics(),
  ]);
}

onMounted(load);

async function _deleteProperty(id: number) {
  await deleteRentalProperty(id);
  await load();
}

async function _addProperty(request: RentalPropertyRequest) {
  await createProperty(request);
  await load();
}
</script>
