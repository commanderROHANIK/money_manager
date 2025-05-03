<template>
    <div class="p-4">
      <h2 class="text-2xl font-bold mb-4">Rental Properties</h2>
  
      <form @submit.prevent="addRentalProperty" class="mb-4">
        <input v-model="newProperty.propertyName" placeholder="Name" class="p-2 border rounded mr-2" required />
        <input v-model="newProperty.address" placeholder="Address" class="p-2 border rounded mr-2" required />
        <input v-model.number="newProperty.rentAmount" placeholder="Rent Amount" class="p-2 border rounded mr-2" type="number" required />
        <input v-model="newProperty.rentDueDate" type="date" class="p-2 border rounded mr-2" required />
        <label><input v-model="newProperty.isRented" type="checkbox" class="mr-1" />Is Rented</label>
        <button type="submit" class="ml-2 bg-green-500 text-white p-2 rounded">Add</button>
      </form>
  
      <ul>
        <li v-for="prop in properties" :key="prop.id" class="mb-2 bg-white p-4 rounded shadow">
          <strong>{{ prop.propertyName }}</strong> – {{ prop.address }} – Rent: {{ prop.rentAmount }} due {{ prop.rentDueDate.split('T')[0] }}
          <span v-if="prop.isRented" class="text-blue-600 ml-2">(Rented)</span>
          <button @click="removeProperty(prop.id)" class="ml-4 text-red-500">Delete</button>
        </li>
      </ul>
    </div>
  </template>
  
  <script setup lang="ts">
  import { ref, onMounted } from 'vue';
  import type { RentalProperty } from '../models/models';
  import { fetchRentalProperties, createRentalProperty, deleteRentalProperty } from '../services/api';
  
  const properties = ref<RentalProperty[]>([]);
  const newProperty = ref<RentalProperty>({
    id: 0,
    propertyName: '',
    address: '',
    rentAmount: 0,
    rentDueDate: '',
    isRented: false,
  });
  
  onMounted(async () => {
    properties.value = await fetchRentalProperties();
  });
  
  async function addRentalProperty() {
    const created = await createRentalProperty(newProperty.value);
    properties.value.push(created);
    newProperty.value = {
      id: 0,
      propertyName: '',
      address: '',
      rentAmount: 0,
      rentDueDate: '',
      isRented: false,
    };
  }
  
  async function removeProperty(id: number) {
    await deleteRentalProperty(id);
    properties.value = properties.value.filter(p => p.id !== id);
  }
  </script>
  