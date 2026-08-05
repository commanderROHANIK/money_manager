<template>
  <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4 p-4">
    <!-- Total Balance Widget -->
    <TotalBalanceWidget />

    <!-- Bank Accounts List Widget -->
    <BaseCard title="Connected Bank Accounts" class="col-span-1 md:col-span-2">
      <template #actions>
        <BaseButton size="sm" @click="showAddModal = true">+ Add Account</BaseButton>
      </template>
      <ul class="text-sm">
        <ListRow v-for="account in bankAccounts" :key="account.id">
          <template #title>
            <p class="font-medium">{{ account.accountName }} - {{ account.bankName }}</p>
          </template>
          <template #subtitle>
            <p class="text-xs text-text-muted">
              {{ account.accountType }} • <span class="tabular-nums">{{ formatCurrency(account.balance) }}</span>
            </p>
          </template>
          <template #trailing>
            <button
              class="text-danger hover:text-danger/70 transition"
              @click="deleteAccount(account.id)"
            >
              ➖
            </button>
          </template>
        </ListRow>
      </ul>
    </BaseCard>

    <!-- Pie Chart Widget -->
    <BaseCard title="Balance Distribution" class="col-span-1">
      <BankAccountPieChart :accounts="sortedAccounts" />
    </BaseCard>

    <!-- Add Modal (Placeholder) -->
    <div v-if="showAddModal" class="fixed inset-0 bg-black/40 flex justify-center items-center z-50">
      <div class="bg-surface border border-border rounded-lg shadow-card p-6 w-96">
        <h3 class="font-heading text-lg font-bold mb-4">Add Bank Account</h3>
        <!-- Form fields go here -->
        <BaseButton @click="showAddModal = false">Close</BaseButton>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchBankAccounts, deleteBankAccount } from '../services/api';
import type { BankAccount } from '../models/models';
import TotalBalanceWidget from '../components/Widgets/BankAccounts/TotalBalance.vue';
import BankAccountPieChart from '../components/Widgets/BankAccounts/BankAccountPieChart.vue'; // You’ll create this
import BaseCard from './ui/BaseCard.vue';
import BaseButton from './ui/BaseButton.vue';
import ListRow from './ui/ListRow.vue';

const bankAccounts = ref<BankAccount[]>([]);
const showAddModal = ref(false);

onMounted(async () => {
  bankAccounts.value = await fetchBankAccounts();
});

async function deleteAccount(id: number) {
  await deleteBankAccount(id);
  bankAccounts.value = bankAccounts.value.filter(acc => acc.id !== id);
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('hu-HU', {
    style: 'currency',
    currency: 'HUF',
    maximumFractionDigits: 0,
  }).format(amount);
}

const sortedAccounts = computed(() => {
  return bankAccounts.value.sort((a, b) => b.balance - a.balance);
});
</script>
