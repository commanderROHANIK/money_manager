<script setup lang="ts">
import { Pie } from 'vue-chartjs';
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  ArcElement
} from 'chart.js';
import type {
  ChartData,
  ChartOptions
} from 'chart.js';
import { computed, defineProps } from 'vue';
import type { BankAccount } from '../../models/models';

ChartJS.register(Title, Tooltip, Legend, ArcElement);

const props = defineProps<{
  accounts: BankAccount[]
}>();

// Chart Data
const data = computed<ChartData<'pie'>>(() => {
  return {
    labels: props.accounts.map(acc => acc.accountName),
    datasets: [
      {
        data: props.accounts.map(acc => acc.balance),
        backgroundColor: [
          '#e74c3c', // Alizarin Red
          '#f39c12', // Orange
          '#f1c40f', // Bright Yellow
          '#2ecc71', // Emerald Green
          '#1abc9c', // Aqua
          '#3498db', // Bright Blue
          '#9b59b6', // Amethyst Purple
          '#e67e22', // Carrot Orange
          '#16a085', // Dark Aqua
          '#2980b9', // Belize Hole Blue
        ],
        borderWidth: 4,
        borderColor: '#ffffff' // White border for separation
      }
    ]
  };
});

// Chart Options
const options: ChartOptions<'pie'> = {
  responsive: true,
  plugins: {
    legend: {
      position: 'bottom',
      labels: {
        font: { size: 14 }
      }
    },
    tooltip: {
      callbacks: {
        label: function (context) {
          const label = context.label || '';
          const value = context.formattedValue || '';
          return `${label}: ${value} Ft`;
        }
      }
    }
  },
  cutout: '78%' // Makes it a donut shape
};
</script>

<template>
  <div>
    <Pie :data="data" :options="options" />
  </div>
</template>
