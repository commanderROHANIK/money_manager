<template>
  <div class="p-4 w-64 h-64 flex flex-col justify-between">
    <h2 class="text-md font-semibold text-center">Loan Status</h2>
    <Doughnut
      v-if="loans.length"
      :data="chartData"
      :options="chartOptions"
      class="flex-1"
    />
    <p v-else class="text-gray-500 text-center text-sm">No data available.</p>
  </div>
</template>


<script setup lang="ts">
import { Doughnut } from 'vue-chartjs';
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js';
import { computed } from 'vue';
import type { Loan } from '../../models/models';

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
      backgroundColor: ['#22c55e', '#ef4444'],
      borderWidth: 2,
      borderColor: '#ffffff',
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
      position: 'bottom',
      labels: {
        color: '#333',
        font: { size: 12, weight: 'bold' },
      },
    },
    tooltip: {
      callbacks: {
        label: (context: any) => {
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
