<template>
  <div class="p-4 space-y-6">
    <!-- Top row: KPIs -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <TotalPortfolioValueWidget />
      <!--
        The one widget that spans two sections: it reads the bank balance to put the holdings
        next to the cash. With banking switched off that half answers 404, so the comparison has
        nothing to compare — better absent than showing invested against a blank.
      -->
      <CashVsInvestedWidget v-if="featureFlags.banking" />
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

    <!-- Add Holding -->
    <BaseCard>
      <AddStockWidget @create="_addStock" />
    </BaseCard>

    <!-- Holdings Table -->
    <div>
      <HoldingsListWidget :key="holdingsVersion" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import TotalPortfolioValueWidget from '../components/Widgets/Stocks/TotalPortfolioValueWidget.vue';
import CashVsInvestedWidget from './Widgets/Stocks/CashVsInvestedWidget.vue';
import { featureFlags } from '../services/features';
import PortfolioPerformanceChartWidget from './Widgets/Stocks/PortfolioPerformanceChartWidget.vue';
import SectorDistributionPieWidget from '../components/Widgets/Stocks/SectorDistributionPieWidget.vue';
import TopGainersAndLosersWidget from '../components/Widgets/Stocks/TopGainersAndLosersWidget.vue';
import DividendIncomeWidget from '../components/Widgets/Stocks/DividendIncomeWidget.vue';
import HoldingsListWidget from '../components/Widgets/Stocks/HoldingsListWidget.vue';
import AddStockWidget from './Widgets/Stocks/AddStockWidget.vue';
import BaseCard from './ui/BaseCard.vue';
import { createStock } from '../services/api';
import type { Stock } from '../models/models';

// HoldingsListWidget fetches and deletes its own holdings and has no exposed refetch, so a
// create here — the one action it cannot see itself — forces a fresh mount to pick it up.
// Delete stays entirely inside the widget, same as it already owns its own fetch.
const holdingsVersion = ref(0);

async function _addStock(payload: Omit<Stock, 'id'>) {
  await createStock({ id: 0, ...payload });
  holdingsVersion.value++;
}
</script>
