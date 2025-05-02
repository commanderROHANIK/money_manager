<template>
  <div class="p-4">
    <h2 class="text-2xl font-bold mb-4">Upcoming Events</h2>

    <form @submit.prevent="createEvent" class="mb-4">
      <input v-model="newEvent.title" placeholder="Title" class="p-2 border rounded mr-2" required />
      <input v-model="newEvent.description" placeholder="Description" class="p-2 border rounded mr-2" />
      <input v-model="newEvent.eventDate" type="datetime-local" class="p-2 border rounded mr-2" required />
      <label class="mr-2">
        <input type="checkbox" v-model="newEvent.isRecurring" />
        Recurring
      </label>
      <label class="mr-2">
        <input type="checkbox" v-model="newEvent.isNotified" />
        Notified
      </label>
      <button type="submit" class="p-2 bg-blue-500 text-white rounded">Create Event</button>
    </form>

    <ul>
      <li v-for="event in events" :key="event.id" class="mb-2">
        <div class="bg-white p-4 rounded shadow">
          <strong>{{ event.title }}</strong> – <span>{{ formatDate(event.eventDate) }}</span>
          <p class="text-sm text-gray-600">{{ event.description }}</p>
          <p class="text-xs">Recurring: {{ event.isRecurring ? 'Yes' : 'No' }}, Notified: {{ event.isNotified ? 'Yes' : 'No' }}</p>
          <button @click="editEvent(event)" class="ml-2 text-blue-500">Edit</button>
          <button @click="deleteEvent(event.id)" class="ml-2 text-red-500">Delete</button>
        </div>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import {
  fetchUpcomingEvents,
  createUpcomingEvent,
  updateUpcomingEvent,
  deleteUpcomingEvent
} from '../services/api'
import type { UpcomingEvent } from '../models/models'

const events = ref<UpcomingEvent[]>([])
const loading = ref(true)
const error = ref('')

// initial new event model
const newEvent = ref<UpcomingEvent>({
  id: 0,
  title: '',
  description: '',
  eventDate: '',
  isRecurring: false,
  isNotified: false,
})

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

async function createEvent() {
  try {
    const created = await createUpcomingEvent(newEvent.value)
    events.value.push(created)
    newEvent.value = {
      id: 0,
      title: '',
      description: '',
      eventDate: '',
      isRecurring: false,
      isNotified: false,
    }
  } catch (err) {
    error.value = 'Failed to create event.'
    console.error(err)
  }
}

async function editEvent(event: UpcomingEvent) {
  const updatedEvent = {
    ...event,
    title: event.title + ' (Edited)', // Example edit
  }

  try {
    await updateUpcomingEvent(event.id, updatedEvent)
    events.value = events.value.map(e => (e.id === event.id ? updatedEvent : e))
  } catch (err) {
    error.value = 'Failed to update event.'
    console.error(err)
  }
}

async function deleteEvent(id: number) {
  try {
    await deleteUpcomingEvent(id)
    events.value = events.value.filter(e => e.id !== id)
  } catch (err) {
    error.value = 'Failed to delete event.'
    console.error(err)
  }
}
</script>
