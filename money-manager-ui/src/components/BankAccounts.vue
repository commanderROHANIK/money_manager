<template>
  <div class="p-4">
    <h2 class="text-2xl font-bold mb-4">Manage Bank Accounts</h2>

    <!-- Create Bank Account Form -->
    <form @submit.prevent="_createBankAccount" class="mb-4">
      <input v-model="newBankAccount.accountName" placeholder="Account Name" class="p-2 border rounded mr-2" required />
      <input v-model="newBankAccount.balance" type="number" placeholder="Balance" class="p-2 border rounded mr-2" required />
      <input v-model="newBankAccount.bankName" placeholder="Bank Name" class="p-2 border rounded mr-2" required />
      <input v-model="newBankAccount.accountNumber" placeholder="Account Number" class="p-2 border rounded mr-2" required />
      <input v-model="newBankAccount.accountType" placeholder="Account Type (Checking/Savings)" class="p-2 border rounded" required />
      <button type="submit" class="p-2 bg-blue-500 text-white rounded">Create Bank Account</button>
    </form>

    <!-- Display Bank Accounts -->
    <ul>
      <li v-for="bankAccount in bankAccounts" :key="bankAccount.id" class="mb-2">
        <div class="bg-white p-4 rounded shadow">
          <strong>{{ bankAccount.accountName }}</strong> - 
          <span>{{ bankAccount.bankName }}</span> - 
          <span>{{ bankAccount.accountType }}</span> - 
          <span>{{ bankAccount.balance }}</span>
          <button @click="editBankAccount(bankAccount)" class="ml-2 text-blue-500">Edit</button>
          <button @click="_deleteBankAccount(bankAccount.id)" class="ml-2 text-red-500">Delete</button>
        </div>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { fetchBankAccounts, createBankAccount, updateBankAccount, deleteBankAccount } from '../services/api';
import type { BankAccount } from '../models/models';

const bankAccounts = ref<BankAccount[]>([]);
const newBankAccount = ref<BankAccount>({
  id: 0,
  accountName: '',
  balance: 0,
  bankName: '',
  accountNumber: '',
  accountType: '',
});

onMounted(async () => {
  try {
    bankAccounts.value = await fetchBankAccounts();
  } catch (err) {
    console.error('Failed to load bank accounts', err);
  }
});

async function _createBankAccount() {
  try {
    const createdBankAccount = await createBankAccount(newBankAccount.value);
    bankAccounts.value.push(createdBankAccount);  // Add the newly created account to the list
    newBankAccount.value = { id: 0, accountName: '', balance: 0, bankName: '', accountNumber: '', accountType: '' };
  } catch (err) {
    console.error('Failed to create bank account', err);
  }
}

async function editBankAccount(bankAccount: BankAccount) {
  const updatedBankAccount: BankAccount = { ...bankAccount, accountName: 'Updated Account Name' };  // Example
  
  try {
    await updateBankAccount(bankAccount.id, updatedBankAccount);
    bankAccounts.value = bankAccounts.value.map(acc => acc.id === bankAccount.id ? updatedBankAccount : acc);
  } catch (err) {
    console.error('Failed to update bank account', err);
  }
}

async function _deleteBankAccount(id: number) {
  try {
    await deleteBankAccount(id);
    bankAccounts.value = bankAccounts.value.filter(acc => acc.id !== id);
  } catch (err) {
    console.error('Failed to delete bank account', err);
  }
}
</script>
