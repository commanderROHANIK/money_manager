<template>
  <div class="p-4 rounded-lg shadow bg-white dark:bg-gray-800">
    <h2 class="text-xl font-semibold mb-4">Rent Collected by Month</h2>

    <Bar :data="chartData" :options="chartOptions" />
  </div>
</template>

<script setup lang="ts">
import {
  Chart as ChartJS,
  BarElement,
  CategoryScale,
  LinearScale,
  Tooltip,
  Legend
} from 'chart.js';
import { Bar } from 'vue-chartjs';
import type { RentalProperty } from '../../models/models';
import { computed, defineProps } from 'vue';


ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend);

const props = defineProps<{
  properties: RentalProperty[];
}>();

// Group rent by month
function groupRentByMonth(properties: RentalProperty[]) {
  const monthlyTotals: Record<string, number> = {};

//   for (const property of properties) {
//     for (const payment of property.rentHistory) {
//       const month = new Date(payment.datePaid).toLocaleString('default', {
//         year: 'numeric',
//         month: 'short'
//       });

//       monthlyTotals[month] = (monthlyTotals[month] || 0) + payment.amount;
//     }
//   }

  return monthlyTotals;
}

const rentByMonth = computed(() => groupRentByMonth(props.properties));

const chartData = computed(() => {
  const labels = Object.keys(rentByMonth.value).sort(
    (a, b) => new Date(a).getTime() - new Date(b).getTime()
  );
  const data = labels.map((label) => rentByMonth.value[label]);

  return {
    labels,
    datasets: [
      {
        label: 'Rent Collected',
        backgroundColor: '#4ADE80',
        borderRadius: 6,
        data
      }
    ]
  };
});

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  scales: {
    y: {
      beginAtZero: true,
      ticks: {
        callback: (value: number) =>
          new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: 'USD',
            maximumFractionDigits: 0
          }).format(value)
      }
    }
  },
  plugins: {
    legend: {
      display: false
    }
  }
};
</script>

<style scoped>
/* Optional: set a fixed height for the chart */
div {
  height: 300px;
}
</style>
