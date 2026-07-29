<template>
  <div>
    <h2 class="text-lg font-semibold mb-4">Rent over time</h2>

    <div v-if="hasData" class="chart-box">
      <Line :data="chartData" :options="chartOptions" />
    </div>
    <p v-else class="text-sm text-gray-500">
      No rent history yet. It fills in automatically as tenancies start and rents change.
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { Line } from 'vue-chartjs';
import {
  Chart as ChartJS,
  LineElement,
  PointElement,
  CategoryScale,
  LinearScale,
  Tooltip,
  Legend,
} from 'chart.js';
import type { ChartOptions } from 'chart.js';
import { RentPriceSource, type RentPricePoint } from '../../../models/models';
import { formatMoney } from '../../../utils/money';

ChartJS.register(LineElement, PointElement, CategoryScale, LinearScale, Tooltip, Legend);

const props = defineProps<{ history: RentPricePoint[]; currencyCode: string }>();

const hasData = computed(() => props.history.length > 0);

/** Every date either series touches, so both plot against one axis. */
const labels = computed(() =>
  [...new Set(props.history.map((p) => p.effectiveFrom.split('T')[0]))].sort()
);

/**
 * Rent holds its value until it is changed, so each series is carried forward across dates
 * where only the other series has a point. A plain scatter would imply the rent dropped to
 * nothing between changes.
 */
function stepSeries(source: RentPriceSource): (number | null)[] {
  const points = props.history
    .filter((p) => p.source === source)
    .sort((a, b) => a.effectiveFrom.localeCompare(b.effectiveFrom));

  if (points.length === 0) return labels.value.map(() => null);

  let current: number | null = null;
  let index = 0;

  return labels.value.map((label) => {
    while (index < points.length && points[index].effectiveFrom.split('T')[0] <= label) {
      current = points[index].amount;
      index++;
    }
    return current;
  });
}

const chartData = computed(() => ({
  labels: labels.value,
  datasets: [
    {
      label: 'Rent charged',
      data: stepSeries(RentPriceSource.Contracted),
      borderColor: '#10B981',
      backgroundColor: '#10B981',
      stepped: true,
      tension: 0,
    },
    {
      label: 'Market estimate',
      data: stepSeries(RentPriceSource.MarketEstimate),
      borderColor: '#F59E0B',
      backgroundColor: '#F59E0B',
      borderDash: [6, 4],
      stepped: true,
      tension: 0,
    },
  ],
}));

const chartOptions = computed<ChartOptions<'line'>>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  spanGaps: true,
  scales: {
    y: {
      beginAtZero: false,
      ticks: {
        callback: (value) => formatMoney(Number(value), props.currencyCode),
      },
    },
  },
  plugins: {
    legend: { position: 'bottom' as const },
  },
}));
</script>

<style scoped>
.chart-box {
  height: 280px;
}
</style>
