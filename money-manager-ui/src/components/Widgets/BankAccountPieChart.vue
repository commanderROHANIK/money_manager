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
  const total = props.accounts.reduce((sum, acc) => sum + acc.balance, 0);
  return {
    labels: props.accounts.map(acc => acc.accountName),
    datasets: [
      {
        data: props.accounts.map(acc => acc.balance),
        backgroundColor: [
          '#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF', '#FF9F40',
          '#B3E283', '#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF'
        ],
        borderWidth: 1
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
  }
};
</script>

<template>
  <div>
    <Pie :data="data" :options="options" />
  </div>
</template>
