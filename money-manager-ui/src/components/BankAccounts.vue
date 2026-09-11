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
              {{ account.accountType }} • <span class="tabular-nums">{{ formatMoney(account.balance, account.currencyCode) }}</span>
            </p>
          </template>
          <template #trailing>
            <button
              class="text-danger hover:text-danger/70 transition"
              :aria-label="t('bankAccount.delete', { name: account.accountName })"
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

    <!-- Add Modal -->
    <div v-if="showAddModal" class="fixed inset-0 bg-black/40 flex justify-center items-center z-50">
      <div class="bg-surface border border-border rounded-lg shadow-card p-6 w-96">
        <AddBankAccountWidget @create="_addAccount" />
        <BaseButton variant="secondary" class="mt-3" block @click="showAddModal = false">
          {{ t('bankAccount.close') }}
        </BaseButton>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { fetchBankAccounts, createBankAccount, deleteBankAccount } from '../services/api';
import type { BankAccount } from '../models/models';
import { formatMoney } from '../utils/money';
import TotalBalanceWidget from '../components/Widgets/BankAccounts/TotalBalance.vue';
import BankAccountPieChart from '../components/Widgets/BankAccounts/BankAccountPieChart.vue';
import AddBankAccountWidget from '../components/Widgets/BankAccounts/AddBankAccountWidget.vue';
import BaseCard from './ui/BaseCard.vue';
import BaseButton from './ui/BaseButton.vue';
import ListRow from './ui/ListRow.vue';

const { t } = useI18n();

const bankAccounts = ref<BankAccount[]>([]);
const showAddModal = ref(false);

async function load() {
  bankAccounts.value = await fetchBankAccounts();
}

onMounted(load);

async function deleteAccount(id: number) {
  try {
    await deleteBankAccount(id);
    await load();
  } catch (error) {
    console.error('Failed to delete bank account:', error);
  }
}

async function _addAccount(payload: Omit<BankAccount, 'id'>) {
  await createBankAccount({ id: 0, ...payload });
  await load();
  showAddModal.value = false;
}

const sortedAccounts = computed(() => {
  // Copy before sorting: Array.prototype.sort mutates in place, so sorting bankAccounts.value
  // directly would have this computed write to its own reactive dependency — permanently
  // reordering the source list and risking a re-evaluation loop.
  return [...bankAccounts.value].sort((a, b) => b.balance - a.balance);
});
</script>
