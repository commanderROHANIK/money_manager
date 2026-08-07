<template>
  <div class="p-4 w-64 h-64 flex flex-col justify-between">
    <h2 class="font-heading text-md font-semibold text-center">Loan Status</h2>
    <Doughnut
      v-if="loans.length"
      :data="chartData"
      :options="chartOptions"
      class="flex-1"
    />
    <p v-else class="text-text-muted text-center text-sm">No data available.</p>
  </div>
</template>


<script setup lang="ts">
import { Doughnut } from 'vue-chartjs';
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js';
import type { TooltipItem } from 'chart.js';
import { computed } from 'vue';
import type { Loan } from '../../../models/models';
import { chartColors } from '../../../utils/chartTheme';

ChartJS.register(ArcElement, Tooltip, Legend);

// ✅ Accept as prop
const props = defineProps<{ loans: Loan[] }>();

const paidOffCount = computed(() => props.loans.filter(l => l.isPaidOff).length);
const remainingCount = computed(() => props.loans.filter(l => !l.isPaidOff).length);

const chartData = computed(() => ({
  labels: ['Paid Off', 'Remaining'],
  datasets: [
    {
      data: [paidOffCount.value, remainingCount.value],
      backgroundColor: [chartColors.primary, chartColors.danger],
      borderWidth: 2,
      borderColor: chartColors.surface,
      hoverOffset: 10,
    },
  ],
}));

const chartOptions = {
  cutout: '70%',
  responsive: true,
  plugins: {
    legend: {
      display: true,
      position: 'bottom' as const,
      labels: {
        color: chartColors.text,
        font: { size: 12, weight: 'bold' as const },
      },
    },
    tooltip: {
      callbacks: {
        label: (context: TooltipItem<'doughnut'>) => {
          const label = context.label || '';
          const value = context.parsed || 0;
          const total = context.dataset.data.reduce((acc: number, v: number) => acc + v, 0);
          const percentage = total ? ((value / total) * 100).toFixed(1) : '0';
          return `${label}: ${percentage}%`;
        },
      },
    },
  },
};
</script>
