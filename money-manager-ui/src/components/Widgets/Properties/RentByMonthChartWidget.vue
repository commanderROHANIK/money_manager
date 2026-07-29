<template>
  <div class="p-4 rounded-lg shadow bg-white chart-box">
    <h2 class="text-lg font-semibold mb-4">Rent Collected by Month</h2>

    <Bar v-if="hasData" :data="chartData" :options="chartOptions" />
    <p v-else class="text-sm text-gray-500">
      No rent payments recorded yet for these {{ properties.length }} properties.
    </p>
  </div>
</template>

<script setup lang="ts">
import {
  Chart as ChartJS,
  BarElement,
  CategoryScale,
  LinearScale,
  Tooltip,
  Legend
} from 'chart.js';
import type { ChartOptions } from 'chart.js';
import { Bar } from 'vue-chartjs';
import { computed } from 'vue';
import type { RentalProperty } from '../../../models/models';
import { formatMoney } from '../../../utils/money';

ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend);

/**
 * A rent payment that has actually been received. Until the transaction ledger exists there
 * is no source for these, so the widget renders an empty state rather than a blank chart.
 */
export interface RentPayment {
  datePaid: string;
  amount: number;
  currencyCode: string;
}

const props = withDefaults(
  defineProps<{
    properties: RentalProperty[];
    payments?: RentPayment[];
  }>(),
  { payments: () => [] }
);

const currency = computed(
  () => props.payments[0]?.currencyCode ?? props.properties[0]?.currencyCode ?? 'EUR'
);

const rentByMonth = computed(() => {
  const monthlyTotals: Record<string, number> = {};

  for (const payment of props.payments) {
    const month = new Date(payment.datePaid).toLocaleString('default', {
      year: 'numeric',
      month: 'short'
    });
    monthlyTotals[month] = (monthlyTotals[month] || 0) + payment.amount;
  }

  return monthlyTotals;
});

const hasData = computed(() => Object.keys(rentByMonth.value).length > 0);

const chartData = computed(() => {
  const labels = Object.keys(rentByMonth.value).sort(
    (a, b) => new Date(a).getTime() - new Date(b).getTime()
  );

  return {
    labels,
    datasets: [
      {
        label: 'Rent Collected',
        backgroundColor: '#4ADE80',
        borderRadius: 6,
        data: labels.map((label) => rentByMonth.value[label])
      }
    ]
  };
});

const chartOptions = computed<ChartOptions<'bar'>>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  scales: {
    y: {
      beginAtZero: true,
      ticks: {
        callback: (value) => formatMoney(Number(value), currency.value)
      }
    }
  },
  plugins: {
    legend: { display: false }
  }
}));
</script>

<style scoped>
.chart-box {
  height: 300px;
}
</style>
