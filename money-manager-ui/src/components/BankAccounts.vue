<template>
  <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4 p-4">
    <!-- Total Balance Widget -->
    <TotalBalanceWidget />

    <!-- Bank Accounts List Widget -->
    <div class="bg-white p-6 rounded-2xl shadow-md col-span-1 md:col-span-2">
      <div class="flex justify-between items-center mb-2">
        <h2 class="text-lg font-semibold">Connected Bank Accounts</h2>
        <button
          class="bg-green-500 hover:bg-green-600 text-white text-sm px-3 py-1 rounded"
          @click="showAddModal = true"
        >
          + Add Account
        </button>
      </div>
      <ul class="divide-y text-sm">
        <li
          v-for="account in bankAccounts"
          :key="account.id"
          class="flex justify-between items-center py-2"
        >
          <div>
            <p class="font-medium">{{ account.accountName }} - {{ account.bankName }}</p>
            <p class="text-gray-500 text-xs">{{ account.accountType }} • {{ formatCurrency(account.balance) }}</p>
          </div>
          <button
            class="text-red-500 hover:text-red-700"
            @click="deleteAccount(account.id)"
          >
            ➖
          </button>
        </li>
      </ul>
    </div>

    <!-- Pie Chart Widget -->
    <div class="bg-white p-6 rounded-2xl shadow-md col-span-1">
      <h2 class="text-lg font-semibold mb-2">Balance Distribution</h2>
      <BankAccountPieChart :accounts="sortedAccounts" />
    </div>

    <!-- Add Modal (Placeholder) -->
    <div v-if="showAddModal" class="fixed inset-0 bg-black bg-opacity-40 flex justify-center items-center z-50">
      <div class="bg-white rounded-xl shadow-lg p-6 w-96">
        <h3 class="text-lg font-semibold mb-4">Add Bank Account</h3>
        <!-- Form fields go here -->
        <button class="bg-blue-500 text-white px-4 py-1 rounded" @click="showAddModal = false">Close</button>
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
