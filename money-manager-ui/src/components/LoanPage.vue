<template>
  <div class="p-4 grid grid-cols-1 xl:grid-cols-3 gap-4">
    <!-- Top Row -->
    <BaseCard class="col-span-1 xl:col-span-1">
      <TotalLoanAmountWidget :loans="loans" />
    </BaseCard>

    <BaseCard class="col-span-1 xl:col-span-1">
      <LoanStatusPieWidget :loans="loans" />
    </BaseCard>

    <!-- Monthly Repayment Chart -->
    <BaseCard class="col-span-1 xl:col-span-1">
      <MonthlyRepaymentChartWidget :accounts="loans" />
    </BaseCard>

    <!-- Loans List -->
    <BaseCard class="col-span-1 xl:col-span-3">
      <LoanListWidget :loans="loans" @delete-loan="_deleteLoan" />
    </BaseCard>

    <!-- Add Loan -->
    <BaseCard
      ref="addLoanCard"
      class="col-span-1 xl:col-span-3"
      :class="{ 'ring-2 ring-primary-strong': isActive('loan') }"
    >
      <p v-if="isActive('loan')" class="mb-3 text-sm text-primary-strong">
        {{ t('onboarding.spotlight.loan') }}
      </p>
      <AddLoanWidget @create="_addLoan" />
    </BaseCard>

    <!-- Bottom Row -->
    <BaseCard class="col-span-1">
      <NextDueRepaymentWidget :loans="loans" />
    </BaseCard>
    <BaseCard class="col-span-1 xl:col-span-2">
      <TopLoansWidget :loans="loans" />
    </BaseCard>
  </div>
</template>

<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { fetchLoans, createLoan, deleteLoan } from '../services/api';
import type { Loan } from '../models/models';
import { useOnboardingSpotlight } from '../composables/useOnboardingSpotlight';

// Widgets
import TotalLoanAmountWidget from '../components/Widgets/Loans/TotalLoanAmountWidget.vue';
import LoanStatusPieWidget from '../components/Widgets/Loans/LoanStatusPieWidget.vue';
import MonthlyRepaymentChartWidget from '../components/Widgets/Loans/MonthlyRepaymentChartWidget.vue';
import LoanListWidget from '../components/Widgets/Loans/LoanListWidget.vue';
import AddLoanWidget from '../components/Widgets/Loans/AddLoanWidget.vue';
import NextDueRepaymentWidget from '../components/Widgets/Loans/NextDueRepaymentWidget.vue';
import TopLoansWidget from '../components/Widgets/Loans/TopLoansWidget.vue';
import BaseCard from './ui/BaseCard.vue';

const { t } = useI18n();
const { isActive, clear } = useOnboardingSpotlight(['loan']);
const addLoanCard = ref<InstanceType<typeof BaseCard> | null>(null);

const loans = ref<Loan[]>([]);

async function load() {
  loans.value = await fetchLoans();
}

onMounted(async () => {
  await load();

  if (isActive('loan')) {
    await nextTick();
    addLoanCard.value?.$el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
});

async function _deleteLoan(id: number) {
  await deleteLoan(id);
  await load();
}

async function _addLoan(payload: Omit<Loan, 'id'>) {
  await createLoan({ id: 0, ...payload });
  clear();
  await load();
}
</script>
