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
import type { Loan } from '../../../models/models';
import { chartCategoricalPalette, chartColors } from '../../../utils/chartTheme';

ChartJS.register(Title, Tooltip, Legend, ArcElement);

const props = defineProps<{
  accounts: Loan[]
}>();

// Chart Data
const data = computed<ChartData<'pie'>>(() => {
  const palette = chartCategoricalPalette();
  return {
    labels: props.accounts.map(acc => acc.loanName),
    datasets: [
      {
        data: props.accounts.map(acc => acc.loanAmount),
        backgroundColor: props.accounts.map((_, i) => palette[i % palette.length]),
        borderWidth: 2,
        borderColor: chartColors.surface
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
   <div class="p-4 w-64 h-64 flex flex-col justify-between">
    <Pie :data="data" :options="options" />
  </div>
</template>
