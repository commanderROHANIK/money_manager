<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('property.rentedVsVacant.title') }}</h2>
    <div class="chart-box">
      <Doughnut :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { Doughnut } from 'vue-chartjs';
import {
  Chart as ChartJS,
  ArcElement,
  Tooltip,
  Legend
} from 'chart.js';
import type { RentalProperty } from '../../../models/models';
import { chartColors } from '../../../utils/chartTheme';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

ChartJS.register(ArcElement, Tooltip, Legend);

const props = defineProps<{
  properties: RentalProperty[];
}>();

const rentedCount = computed(() => props.properties.filter(p => p.isRented).length);
const vacantCount = computed(() => props.properties.length - rentedCount.value);

const chartData = computed(() => ({
  labels: [t('property.rented'), t('property.vacant')],
  datasets: [
    {
      data: [rentedCount.value, vacantCount.value],
      backgroundColor: [chartColors.primary, chartColors.danger],
      borderColor: [chartColors.surface, chartColors.surface],
      borderWidth: 2
    }
  ]
}));

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      position: 'bottom' as const
    }
  }
};
</script>

<style scoped>
.chart-box {
  height: 300px;
}
</style>
