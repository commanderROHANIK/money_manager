<template>
  <div>
    <h2 class="text-xl font-semibold mb-4">Rented vs Vacant</h2>
    <Doughnut :data="chartData" :options="chartOptions" />
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
import type { RentalProperty } from '../../models/models';

ChartJS.register(ArcElement, Tooltip, Legend);

const props = defineProps<{
  properties: RentalProperty[];
}>();

const rentedCount = computed(() => props.properties.filter(p => p.isRented).length);
const vacantCount = computed(() => props.properties.length - rentedCount.value);

const chartData = computed(() => ({
  labels: ['Rented', 'Vacant'],
  datasets: [
    {
      data: [rentedCount.value, vacantCount.value],
      backgroundColor: ['#10B981', '#F87171'], // green, red
      borderColor: ['#ffffff', '#ffffff'],
      borderWidth: 2
    }
  ]
}));

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      position: 'bottom'
    }
  }
};
</script>

<style scoped>
/* Make sure chart is nicely sized */
div {
  height: 300px;
}
</style>
