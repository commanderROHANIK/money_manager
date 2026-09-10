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

    <BaseCard
      ref="addPropertyCard"
      class="col-span-1 xl:col-span-3"
      :class="{ 'ring-2 ring-primary-strong': isActive('property') }"
    >
      <p v-if="isActive('property')" class="mb-3 text-sm text-primary-strong">
        {{ t('onboarding.spotlight.property') }}
      </p>
      <AddPropertyWidget
        :key="addFormKey"
        :errors="addErrors"
        :error="addError"
        @create="_addProperty"
      />
    </BaseCard>
  </div>
</template>

<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { extractApiError, fetchRentalProperties, deleteRentalProperty } from '../services/api';
import { createProperty, fetchArrears, fetchPortfolioAnalytics } from '../services/propertyApi';
import type { RentalPropertyRequest } from '../services/propertyApi';
import type { PortfolioAnalytics, PropertyArrears, RentalProperty } from '../models/models';
import { useOnboardingSpotlight } from '../composables/useOnboardingSpotlight';

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

const { t } = useI18n();
const { isActive, clear } = useOnboardingSpotlight(['property']);
const addPropertyCard = ref<InstanceType<typeof BaseCard> | null>(null);

const properties = ref<RentalProperty[]>([]);
const portfolio = ref<PortfolioAnalytics | null>(null);
const arrears = ref<PropertyArrears[]>([]);

const addErrors = ref<Record<string, string>>({});
const addError = ref<string | null>(null);

// Bumped after a successful create, which remounts the add form and empties it. A key rather
// than a reset() call on the child: the form's cleared state is just its initial state, so
// remounting expresses it without the widget needing an imperative handle at all.
const addFormKey = ref(0);

async function load() {
  [properties.value, portfolio.value, arrears.value] = await Promise.all([
    fetchRentalProperties(),
    fetchPortfolioAnalytics(),
    fetchArrears(),
  ]);
}

onMounted(async () => {
  await load();

  if (isActive('property')) {
    await nextTick();
    addPropertyCard.value?.$el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
});

async function _deleteProperty(id: number) {
  await deleteRentalProperty(id);
  await load();
}

/**
 * The page owns the request, so it owns what happens to the form afterwards.
 *
 * On success the widget is remounted by bumping its key, which is what empties it — the widget
 * cannot do that itself, because at the moment it emits it has no idea whether the write will be
 * accepted. It used to clear regardless, which meant a rejected write left the user staring at
 * empty inputs with error messages underneath them.
 *
 * On failure the form is left exactly as typed, with the server's messages placed against the
 * fields that caused them.
 */
async function _addProperty(request: RentalPropertyRequest) {
  addErrors.value = {};
  addError.value = null;

  try {
    await createProperty(request);
  } catch (err) {
    const { fields, message } = extractApiError(err);

    addErrors.value = fields;
    // Only when there is no field to hang it on, so the user is not told the same thing twice.
    addError.value = Object.keys(fields).length > 0 ? null : message;

    return;
  }

  addFormKey.value += 1;
  clear();
  await load();
}
</script>
