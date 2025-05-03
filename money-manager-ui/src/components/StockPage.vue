<template>
    <div class="p-4">
      <h2 class="text-2xl font-bold mb-4">Stock Portfolio</h2>
  
      <form @submit.prevent="addStock" class="mb-4">
        <input v-model="newStock.ticker" placeholder="Ticker" class="p-2 border rounded mr-2" required />
        <input v-model.number="newStock.sharesOwned" type="number" placeholder="Shares" class="p-2 border rounded mr-2" required />
        <input v-model.number="newStock.purchasePrice" type="number" placeholder="Purchase Price" class="p-2 border rounded mr-2" required />
        <input v-model.number="newStock.currentPrice" type="number" placeholder="Current Price" class="p-2 border rounded mr-2" required />
        <input v-model="newStock.purchaseDate" type="date" class="p-2 border rounded mr-2" required />
        <button type="submit" class="bg-green-500 text-white p-2 rounded">Add</button>
      </form>
  
      <ul>
        <li v-for="stock in stocks" :key="stock.id" class="mb-2 bg-white p-4 rounded shadow">
          <strong>{{ stock.ticker }}</strong>: {{ stock.sharesOwned }} shares @ {{ stock.purchasePrice }} (now {{ stock.currentPrice }}) – Purchased on {{ stock.purchaseDate.split('T')[0] }}
          <button @click="removeStock(stock.id)" class="ml-4 text-red-500">Delete</button>
        </li>
      </ul>
    </div>
  </template>
  
  <script setup lang="ts">
  import { ref, onMounted } from 'vue';
  import type { Stock } from '../models/models';
  import { fetchStocks, createStock, deleteStock } from '../services/api';
  
  const stocks = ref<Stock[]>([]);
  const newStock = ref<Stock>({
    id: 0,
    ticker: '',
    sharesOwned: 0,
    purchasePrice: 0,
    currentPrice: 0,
    purchaseDate: '',
  });
  
  onMounted(async () => {
    stocks.value = await fetchStocks();
  });
  
  async function addStock() {
    const created = await createStock(newStock.value);
    stocks.value.push(created);
    newStock.value = {
      id: 0,
      ticker: '',
      sharesOwned: 0,
      purchasePrice: 0,
      currentPrice: 0,
      purchaseDate: '',
    };
  }
  
  async function removeStock(id: number) {
    await deleteStock(id);
    stocks.value = stocks.value.filter(s => s.id !== id);
  }
  </script>
  