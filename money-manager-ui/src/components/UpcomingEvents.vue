<template>
    <div class="p-4">
      <h2 class="text-2xl font-bold mb-4">Upcoming Events</h2>
  
      <div v-if="loading" class="text-gray-500">Loading events...</div>
      <div v-else-if="error" class="text-red-500">{{ error }}</div>
      <ul v-else>
        <li
          v-for="event in events"
          :key="event.id"
          class="mb-2"
        >
          <div class="bg-white p-4 rounded-xl shadow-sm border">
            <strong>{{ event.name }}</strong> –
            <span>{{ formatDate(event.eventDate) }}</span>
          </div>
        </li>
      </ul>
    </div>
  </template>
  
  <script setup lang="ts">
  import { ref, onMounted } from 'vue'
  import { fetchUpcomingEvents } from '../services/api'
  import type { UpcomingEvent } from '../models/models'
  
  const events = ref<UpcomingEvent[]>([])
  const loading = ref(true)
  const error = ref('')
  
  onMounted(async () => {
    try {
      events.value = await fetchUpcomingEvents()
    } catch (err) {
      error.value = 'Failed to load events.'
      console.error(err)
    } finally {
      loading.value = false
    }
  })
  
  function formatDate(date: string) {
    return new Date(date).toLocaleString()
  }
  </script>
  