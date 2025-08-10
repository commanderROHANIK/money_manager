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
import type { BankAccount } from '../../../models/models';

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
          '#2ecc71', // Emerald Green
          '#3498db', // Bright Blue
          '#e67e22', // Carrot Orange
          '#e74c3c', // Alizarin Red
          '#f39c12', // Orange
          '#1abc9c', // Aqua
          '#9b59b6', // Amethyst Purple
          '#16a085', // Dark Aqua
          '#2980b9', // Belize Hole Blue
        ],
        borderWidth: 2,
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
