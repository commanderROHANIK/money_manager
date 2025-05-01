<!-- <template>
    <div>
        <div class="min-h-screen bg-gray-100 flex items-center justify-center">
    <div class="bg-green p-6 rounded-lg shadow-md">
      <h1 class="text-2xl font-bold text-blue-600">Hello Tailwind!</h1>
      <p class="mt-2 text-gray-700">Your styling is working 🎉</p>
    </div>
  </div>

      <h2>Bank Accounts</h2>
      <ul>
        <li v-for="account in bankAccounts" :key="account.id">
          {{ account.name }} - {{ account.balance | currency }}
        </li>
      </ul>
    </div>
  </template>
  
  <script lang="ts" setup>
  import { ref, onMounted } from 'vue';
  import api from '../services/api';
  
  interface BankAccount {
    id: number;
    name: string;
    balance: number;
  }
  
  const bankAccounts = ref<BankAccount[]>([]);
  
  const fetchBankAccounts = async () => {
    try {
      const response = await api.get('/bank-accounts');
      bankAccounts.value = response.data;
    } catch (error) {
      console.error('Error fetching bank accounts:', error);
    }
  };
  
  onMounted(() => {
    fetchBankAccounts();
  });
  </script>
  
  <style scoped>
  /* Add custom styles here */
  </style>
   -->