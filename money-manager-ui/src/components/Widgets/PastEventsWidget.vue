<template>
  <div class="widget past-events-widget">
    <h2 class="widget-title">Past Events</h2>

    <ul v-if="pastEvents?.length > 0" class="event-list">
      <li v-for="event in pastEvents" :key="event.id" class="event-item">
        <div class="event-header">
          <h3 class="event-title">{{ event.title }}</h3>
          <span class="event-date">{{ formatDate(event.eventDate) }}</span>
        </div>
        <p class="event-description">{{ event.description }}</p>
      </li>
    </ul>

    <p v-else class="no-events">No past events found.</p>
  </div>
</template>

<script lang="ts" setup>
import { computed } from 'vue'

export interface UpcomingEvent {
  id: number
  title: string
  description: string
  eventDate: string // ISO date string
  isRecurring: boolean
  isNotified: boolean
}

const props = defineProps<{
  events: UpcomingEvent[]
}>()

const pastEvents = computed(() =>
  props.events?.filter(e => new Date(e.eventDate) < new Date())
)

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}
</script>

<style scoped>
.widget {
  background: white;
  padding: 1rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}
.widget-title {
  font-size: 1.25rem;
  font-weight: bold;
  margin-bottom: 1rem;
}
.event-list {
  list-style: none;
  padding: 0;
  margin: 0;
}
.event-item {
  padding: 0.75rem 0;
  border-bottom: 1px solid #eee;
}
.event-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.event-title {
  font-size: 1rem;
  font-weight: 600;
}
.event-date {
  font-size: 0.875rem;
  color: #666;
}
.event-description {
  margin-top: 0.25rem;
  font-size: 0.9rem;
  color: #555;
}
.no-events {
  text-align: center;
  color: #888;
}
</style>
