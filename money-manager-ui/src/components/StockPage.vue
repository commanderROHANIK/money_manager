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
import TotalPortfolioValueWidget from '../components/widgets/TotalPortfolioValueWidget.vue';
import CashVsInvestedWidget from '../components/widgets/CashVsInvestedWidget.vue';
import PortfolioPerformanceChartWidget from '../components/widgets/PortfolioPerformanceChartWidget.vue';
import SectorDistributionPieWidget from '../components/widgets/SectorDistributionPieWidget.vue';
import TopGainersAndLosersWidget from '../components/widgets/TopGainersAndLosersWidget.vue';
import DividendIncomeWidget from '../components/widgets/DividendIncomeWidget.vue';
import HoldingsListWidget from '../components/widgets/HoldingsListWidget.vue';
</script>

<style scoped>
/* Optional: custom styles here */
</style>
