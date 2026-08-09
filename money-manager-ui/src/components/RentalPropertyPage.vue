<template>
  <div class="p-4 grid grid-cols-1 xl:grid-cols-3 gap-4">
    <!-- Portfolio headline -->
    <BaseCard class="col-span-1 xl:col-span-3">
      <PortfolioSummaryWidget :portfolio="portfolio" />
    </BaseCard>

    <!-- Top Row Widgets -->
    <BaseCard class="col-span-1">
      <TotalRentWidget :properties="properties" />
    </BaseCard>

    <BaseCard class="col-span-1">
      <RentedVsVacantPieWidget :properties="properties" />
    </BaseCard>

    <BaseCard class="col-span-1">
      <UpcomingRentDueWidget :properties="properties" />
    </BaseCard>

    <!-- The commercial hook: where rent is trailing the market -->
    <BaseCard class="col-span-1 xl:col-span-2">
      <UnderpricedPropertiesWidget :metrics="portfolio?.properties ?? []" />
    </BaseCard>

    <BaseCard class="col-span-1">
      <MostExpensivePropertyWidget :properties="properties" />
    </BaseCard>

    <!-- Properties List -->
    <BaseCard class="col-span-1 xl:col-span-3">
      <PropertyListWidget
        :properties="properties"
        :arrears="arrears"
        @delete-property="_deleteProperty"
      />
    </BaseCard>

    <BaseCard class="col-span-1 xl:col-span-3">
      <AddPropertyWidget @create="_addProperty" />
    </BaseCard>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { fetchRentalProperties, deleteRentalProperty } from '../services/api';
import { createProperty, fetchArrears, fetchPortfolioAnalytics } from '../services/propertyApi';
import type { RentalPropertyRequest } from '../services/propertyApi';
import type { PortfolioAnalytics, PropertyArrears, RentalProperty } from '../models/models';

// Widgets
import TotalRentWidget from '../components/Widgets/Properties/TotalRentWidget.vue';
import RentedVsVacantPieWidget from '../components/Widgets/Properties/RentedVsVacantPieWidget.vue';
import UpcomingRentDueWidget from '../components/Widgets/Properties/UpcomingRentDueWidget.vue';
import PropertyListWidget from '../components/Widgets/Properties/PropertyListWidget.vue';
import MostExpensivePropertyWidget from '../components/Widgets/Properties/MostExpensivePropertyWidget.vue';
import UnderpricedPropertiesWidget from '../components/Widgets/Properties/UnderpricedPropertiesWidget.vue';
import PortfolioSummaryWidget from '../components/Widgets/Properties/PortfolioSummaryWidget.vue';
import AddPropertyWidget from '../components/Widgets/Properties/AddPropertyWidget.vue';
import BaseCard from './ui/BaseCard.vue';

const properties = ref<RentalProperty[]>([]);
const portfolio = ref<PortfolioAnalytics | null>(null);
const arrears = ref<PropertyArrears[]>([]);

async function load() {
  [properties.value, portfolio.value, arrears.value] = await Promise.all([
    fetchRentalProperties(),
    fetchPortfolioAnalytics(),
    fetchArrears(),
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
