<template>
  <div class="bg-white p-6 rounded-2xl shadow-md">
    <h2 class="text-lg font-semibold mb-2">Events</h2>
    <p class="text-sm">{{ upcomingCount }} Upcoming | {{ pastCount }} Past</p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchUpcomingEvents } from '../../services/api';
import type { UpcomingEvent } from '../../models/models';

const events = ref<UpcomingEvent[]>([]);

onMounted(async () => {
  try {
    events.value = await fetchUpcomingEvents();
  } catch (error) {
    console.error('Failed to load events:', error);
  }
});

const now = new Date();

const upcomingCount = computed(() =>
  events.value.filter(e => new Date(e.eventDate) >= now).length
);

const pastCount = computed(() =>
  events.value.filter(e => new Date(e.eventDate) < now).length
);
</script>
