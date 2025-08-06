<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { Pie } from 'vue-chartjs';
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  ArcElement
} from 'chart.js';
import { fetchStocks } from '../../services/api';
import type { Stock } from '../../models/models';
import type { ChartData, ChartOptions } from 'chart.js';

ChartJS.register(Title, Tooltip, Legend, ArcElement);

const stocks = ref<Stock[]>([]);
const loading = ref(true);

onMounted(async () => {
  try {
    stocks.value = await fetchStocks();
  } finally {
    loading.value = false;
  }
});

// Stub sector classification
function getSector(ticker: string): string {
  if (ticker.startsWith('A')) return 'Technology';
  if (ticker.startsWith('B')) return 'Finance';
  if (ticker.startsWith('C')) return 'Energy';
  return 'Other';
}

const sectorDistribution = computed(() => {
  const result: Record<string, number> = {};

  for (const stock of stocks.value) {
    const sector = getSector(stock.ticker);
    const value = stock.currentPrice * stock.sharesOwned;
    result[sector] = (result[sector] || 0) + value;
  }

  return result;
});

const hasData = computed(() => Object.keys(sectorDistribution.value).length > 0);

const chartData = computed<ChartData<'pie'>>(() => {
  const labels = Object.keys(sectorDistribution.value);
  const data = Object.values(sectorDistribution.value);

  const colors = ['#1abc9c', '#3498db', '#9b59b6', '#f39c12', '#e74c3c', '#7f8c8d'];

  return {
    labels,
    datasets: [
      {
        data,
        backgroundColor: colors.slice(0, labels.length),
        borderColor: '#fff',
        borderWidth: 1
      }
    ]
  };
});

const chartOptions: ChartOptions<'pie'> = {
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
        label: (context) => {
          const label = context.label || '';
          const value = context.parsed || 0;
          return `${label}: ${value.toFixed(0)} Ft`;
        }
      }
    }
  }
};
</script>

<template>
  <div>
    <div v-if="loading">Loading...</div>
    <div v-else-if="!hasData">
      <p class="text-center text-gray-500">No data available to display sector distribution.</p>
    </div>
    <div v-else>
      <Pie :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>
