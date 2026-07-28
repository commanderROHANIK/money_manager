<template>
  <div class="p-6 space-y-6">
    <!-- Top row: Summary stats -->
    <div class="grid grid-cols-1 gap-4 w-full">
      <div class="col-span-1 w-full">
        <EventSummaryStatsWidget />
      </div>
    </div>

    <!-- Second row: Upcoming and Past events -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <UpcomingEventsWidget />
      <PastEventsWidget :events="events" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import EventSummaryStatsWidget from '../components/Widgets/Events/EventSummaryStatsWidget.vue';
import UpcomingEventsWidget from '../components/Widgets/Events/UpcomingEventsWidget.vue';
import PastEventsWidget from './Widgets/Events/PastEventsWidget.vue';
import { fetchUpcomingEvents } from '../services/api';
import type { UpcomingEvent } from '../models/models';

// PastEventsWidget requires an `events` prop but was rendered without one, so it always
// reported "No past events found" no matter what was in the database.
const events = ref<UpcomingEvent[]>([]);

onMounted(async () => {
  try {
    events.value = await fetchUpcomingEvents();
  } catch (error) {
    console.error('Failed to load events:', error);
  }
});
</script>
