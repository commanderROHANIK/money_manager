<template>
  <div class="p-4 grid grid-cols-1 xl:grid-cols-3 gap-4">
    <!-- Top Row -->
    <div class="col-span-1 xl:col-span-1 bg-white p-4 rounded-2xl shadow-md">
      <TotalLoanAmountWidget :loans="loans" />
    </div>
    
    <div class="col-span-1 xl:col-span-1 bg-white p-4 rounded-2xl shadow-md">
      <LoanStatusPieWidget :loans="loans" />
    </div>

    <!-- Monthly Repayment Chart -->
    <div class="col-span-1 xl:col-span-1 bg-white p-4 rounded-2xl shadow-md">
      <MonthlyRepaymentChartWidget :accounts="loans" />
    </div>

    <!-- Loans List -->
    <div class="col-span-1 xl:col-span-3 bg-white p-4 rounded-2xl shadow-md">
       <LoanListWidget :loans="loans" @delete-loan="_deleteLoan" />

    </div>

    <!-- Bottom Row -->
    <div class="col-span-1 bg-white p-4 rounded-2xl shadow-md">
      <NextDueRepaymentWidget :loans="loans" />
    </div>
    <div class="col-span-1 xl:col-span-2 bg-white p-4 rounded-2xl shadow-md">
      <TopLoansWidget :loans="loans" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { fetchLoans, deleteLoan } from '../services/api';
import type { Loan } from '../models/models';

// Widgets
import TotalLoanAmountWidget from '../components/Widgets/TotalLoanAmountWidget.vue';
import LoanStatusPieWidget from '../components/Widgets/LoanStatusPieWidget.vue';
import MonthlyRepaymentChartWidget from '../components/Widgets/MonthlyRepaymentChartWidget.vue';
import LoanListWidget from '../components/Widgets/LoanListWidget.vue';
import NextDueRepaymentWidget from '../components/Widgets/NextDueRepaymentWidget.vue';
// import TopLoansWidget from '../components/Widgets/TopLoansWidget.vue';

const loans = ref<Loan[]>([]);

onMounted(async () => {
  loans.value = await fetchLoans();
});

async function _deleteLoan(id: number) {
  await deleteLoan(id);
  loans.value = loans.value.filter(l => l.id !== id);
}
</script>
