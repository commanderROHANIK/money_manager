<template>
  <div class="p-6 space-y-6">
    <!-- Top row: KPIs -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <TotalPortfolioValueWidget />
      <CashVsInvestedWidget />
    </div>

    <!-- Second row: Charts -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <PortfolioPerformanceChartWidget />
      <SectorDistributionPieWidget />
    </div>

    <!-- Third row: Gainers/Losers + Dividends -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <TopGainersAndLosersWidget />
      <DividendIncomeWidget />
    </div>

    <!-- Holdings Table -->
    <div>
      <HoldingsListWidget />
    </div>
  </div>
</template>

<script setup lang="ts">
// Import placeholder or implemented widgets
import TotalPortfolioValueWidget from '../components/Widgets/Stocks/TotalPortfolioValueWidget.vue';
import CashVsInvestedWidget from './Widgets/Stocks/CashVsInvestedWidget.vue';
import PortfolioPerformanceChartWidget from './Widgets/Stocks/PortfolioPerformanceChartWidget.vue';
import SectorDistributionPieWidget from '../components/Widgets/Stocks/SectorDistributionPieWidget.vue';
import TopGainersAndLosersWidget from '../components/Widgets/Stocks/TopGainersAndLosersWidget.vue';
import DividendIncomeWidget from '../components/Widgets/Stocks/DividendIncomeWidget.vue';
import HoldingsListWidget from '../components/Widgets/Stocks/HoldingsListWidget.vue';
</script>

<style scoped>
/* Optional: custom styles here */
</style>
