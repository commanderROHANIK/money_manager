<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { Line } from 'vue-chartjs';
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  LineElement,
  PointElement,
  LinearScale,
  CategoryScale
} from 'chart.js';
import { fetchStocks } from '../../../services/api';
import type { Stock } from '../../../models/models';
import type { ChartData, ChartOptions } from 'chart.js';
import { chartColors, chartFonts } from '../../../utils/chartTheme';

ChartJS.register(Title, Tooltip, Legend, LineElement, PointElement, LinearScale, CategoryScale);

const stocks = ref<Stock[]>([]);
const loading = ref(true);

onMounted(async () => {
  try {
    stocks.value = await fetchStocks();
  } finally {
    loading.value = false;
  }
});

const data = computed<ChartData<'line'>>(() => {
  if (!stocks.value.length) {
    return {
      labels: [],
      datasets: []
    };
  }

  const labels = stocks.value.map(stock => new Date(stock.purchaseDate).toLocaleDateString());
  const values = stocks.value.map(stock => stock.currentPrice * stock.sharesOwned);

  return {
    labels,
    datasets: [
      {
        label: 'Portfolio Value (Ft)',
        data: values,
        fill: false,
        borderColor: chartColors.primary,
        tension: 0.4,
        pointBackgroundColor: chartColors.primary,
        pointRadius: 5
      }
    ]
  };
});

const options: ChartOptions<'line'> = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      position: 'bottom',
      labels: {
        font: { family: chartFonts.body }
      }
    },
    tooltip: {
      titleFont: { family: chartFonts.body },
      bodyFont: { family: chartFonts.body },
      callbacks: {
        label: (context) => `Value: ${context.formattedValue} Ft`
      }
    }
  },
  scales: {
    x: {
      ticks: { font: { family: chartFonts.body } }
    },
    y: {
      ticks: {
        font: { family: chartFonts.body },
        callback: (value) => `${value} Ft`
      }
    }
  }
};
</script>

<template>
  <div>
    <div v-if="loading" class="text-sm text-text-muted">Loading...</div>
    <div v-else class="h-[280px]">
      <Line :data="data" :options="options" />
    </div>
  </div>
</template>
