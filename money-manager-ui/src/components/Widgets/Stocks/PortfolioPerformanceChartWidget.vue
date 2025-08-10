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
        borderColor: '#3498db',
        tension: 0.4,
        pointBackgroundColor: '#3498db',
        pointRadius: 5
      }
    ]
  };
});

const options: ChartOptions<'line'> = {
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
        label: (context) => `Value: ${context.formattedValue} Ft`
      }
    }
  },
  scales: {
    y: {
      ticks: {
        callback: (value) => `${value} Ft`
      }
    }
  }
};
</script>

<template>
  <div>
    <div v-if="loading">Loading...</div>
    <div v-else>
      <Line :data="data" :options="options" />
    </div>
  </div>
</template>
