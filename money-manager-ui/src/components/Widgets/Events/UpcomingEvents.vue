<template>
  <BaseCard title="Upcoming Events">
    <ul v-if="events.length > 0">
      <ListRow v-for="event in events" :key="event.id">
        <template #title>
          <span class="text-sm font-bold">📅 {{ event.title }}</span>
        </template>
        <template #trailing>
          <span class="font-mono text-xs text-text-muted tabular-nums">{{ formatDate(event.eventDate) }}</span>
          <Badge v-if="event.isRecurring" variant="neutral">Recurring</Badge>
        </template>
      </ListRow>
    </ul>

    <EmptyState v-else title="No upcoming events." />
  </BaseCard>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { fetchUpcomingEvents } from '../../../services/api'; // Adjust the import path as necessary
import type { UpcomingEvent } from '../../../models/models';
import BaseCard from '../../ui/BaseCard.vue';
import ListRow from '../../ui/ListRow.vue';
import Badge from '../../ui/Badge.vue';
import EmptyState from '../../ui/EmptyState.vue';

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
