<template>
  <div class="bg-white p-6 rounded-2xl shadow-md">
    <h2 class="text-lg font-semibold mb-3">Upcoming Events</h2>

    <div v-if="loading" class="text-sm text-gray-500">Loading...</div>
    <div v-else-if="error" class="text-sm text-red-500">Error: {{ error }}</div>
    <div v-else>
      <table v-if="upcoming.length" class="w-full text-sm">
        <thead>
          <tr class="border-b font-semibold text-left">
            <th class="py-2">Title</th>
            <th class="py-2">Description</th>
            <th class="py-2">Date</th>
            <th class="py-2">Recurring</th>
            <th class="py-2">Notified</th>
            <th class="py-2">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="ev in upcoming" :key="ev.id" class="border-b hover:bg-gray-50">
            <td class="py-2">{{ ev.title }}</td>
            <td class="py-2 text-gray-600">{{ ev.description }}</td>
            <td class="py-2">{{ formatDate(ev.eventDate) }}</td>
            <td class="py-2">{{ ev.isRecurring ? 'Yes' : 'No' }}</td>
            <td class="py-2">
              <span :class="ev.isNotified ? 'text-green-600 font-semibold' : 'text-gray-500'">
                {{ ev.isNotified ? 'Notified' : 'Pending' }}
              </span>
            </td>
            <td class="py-2">
              <button
                class="text-sm px-2 py-1 mr-2 rounded bg-blue-500 text-white hover:bg-blue-600 disabled:opacity-50"
                :disabled="ev.isNotified || updatingIds.has(ev.id)"
                @click="markNotified(ev)"
              >
                {{ ev.isNotified ? '✓' : 'Notify' }}
              </button>

              <button
                class="text-sm px-2 py-1 rounded bg-red-100 text-red-700 hover:bg-red-200"
                :disabled="updatingIds.has(ev.id)"
                @click="deleteEvent(ev.id)"
              >
                Delete
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-else class="text-sm text-gray-500">No upcoming events.</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchUpcomingEvents, updateUpcomingEvent, deleteUpcomingEvent } from '../../../services/api';
import type { UpcomingEvent } from '../../../models/models';

const events = ref<UpcomingEvent[]>([]);
const loading = ref(true);
const error = ref<string | null>(null);

// Track ids currently being updated to disable actions while request in flight
const updatingIds = ref(new Set<number>());

onMounted(async () => {
  try {
    const data = await fetchUpcomingEvents();
    events.value = data;
  } catch (err) {
    console.error(err);
    error.value = 'Failed to load events.';
  } finally {
    loading.value = false;
  }
});

// Filter to future events (today or later) and sort ascending
const upcoming = computed(() => {
  const now = new Date();
  return events.value
    .filter(e => {
      const d = new Date(e.eventDate);
      return d >= now;
    })
    .sort((a, b) => new Date(a.eventDate).getTime() - new Date(b.eventDate).getTime());
});

function formatDate(iso: string) {
  const d = new Date(iso);
  return d.toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
}

async function markNotified(ev: UpcomingEvent) {
  if (ev.isNotified) return;
  updatingIds.value.add(ev.id);
  try {
    const updated: UpcomingEvent = { ...ev, isNotified: true };
    await updateUpcomingEvent(ev.id, updated);
    // Update local copy
    const idx = events.value.findIndex(x => x.id === ev.id);
    if (idx !== -1) events.value[idx] = updated;
  } catch (err) {
    console.error('Failed to mark notified', err);
  } finally {
    updatingIds.value.delete(ev.id);
  }
}

async function deleteEvent(id: number) {
  if (!confirm('Delete this event?')) return;
  updatingIds.value.add(id);
  try {
    await deleteUpcomingEvent(id);
    events.value = events.value.filter(e => e.id !== id);
  } catch (err) {
    console.error('Failed to delete event', err);
  } finally {
    updatingIds.value.delete(id);
  }
}
</script>
