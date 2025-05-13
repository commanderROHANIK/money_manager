<template>
    <div class="bg-white p-6 rounded-2xl shadow-md">
      <h2 class="text-lg font-semibold mb-2">Upcoming Events</h2>
  
      <ul v-if="events.length > 0" class="text-sm space-y-1">
        <li v-for="event in events" :key="event.id">
          📅 {{ event.title }} – {{ formatDate(event.eventDate) }}
          <span v-if="event.isRecurring" class="text-gray-500 text-xs">(recurring)</span>
        </li>
      </ul>
  
      <p v-else class="text-gray-500 text-sm">No upcoming events.</p>
    </div>
  </template>
  
  <script setup lang="ts">
  import { ref, onMounted } from 'vue';
  import { fetchUpcomingEvents } from '../../services/api'; // Adjust the import path as necessary
  import type { UpcomingEvent } from '../../models/models';
  
  const events = ref<UpcomingEvent[]>([]);
  
  onMounted(async () => {
    try {
      events.value = await fetchUpcomingEvents();
    } catch (error) {
      console.error('Failed to load upcoming events:', error);
    }
  });
  
  function formatDate(dateStr: string): string {
    const options: Intl.DateTimeFormatOptions = { year: 'numeric', month: 'long', day: 'numeric' };
    return new Date(dateStr).toLocaleDateString('hu-HU', options);
  }
  </script>
  