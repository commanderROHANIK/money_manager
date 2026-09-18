<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('property.rentedVsVacant.title') }}</h2>
    <PieChart :segments="segments" />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { RentalProperty } from '../../../models/models';
import { chartColors } from '../../../utils/chartTheme';
import PieChart from '../../ui/PieChart.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps<{
  properties: RentalProperty[];
}>();

const rentedCount = computed(() => props.properties.filter((p) => p.isRented).length);
const vacantCount = computed(() => props.properties.length - rentedCount.value);

const segments = computed(() => [
  { label: t('property.rented'), value: rentedCount.value, color: chartColors.primary },
  { label: t('property.vacant'), value: vacantCount.value, color: chartColors.danger },
]);
</script>
