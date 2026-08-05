<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">Timeline</h2>

    <p v-if="events.length === 0" class="text-sm text-text-muted">
      Nothing recorded yet.
    </p>

    <ol v-else class="relative border-l border-border ml-2 space-y-4">
      <li v-for="event in events" :key="event.id" class="ml-4">
        <span
          class="absolute -left-1.5 w-3 h-3 rounded-full"
          :class="event.isSystemGenerated ? 'bg-primary' : 'bg-text-muted'"
        />
        <div class="flex items-baseline justify-between gap-2">
          <p class="font-medium">{{ event.title }}</p>
          <time class="text-xs text-text-muted whitespace-nowrap">
            {{ formatDate(event.occurredOn) }}
          </time>
        </div>
        <p v-if="event.description" class="text-sm text-text-muted">{{ event.description }}</p>
        <p class="text-[11px] text-text-muted">{{ PROPERTY_EVENT_LABELS[event.type] }}</p>
      </li>
    </ol>
  </div>
</template>

<script setup lang="ts">
import type { PropertyEvent } from '../../../models/models';
import { PROPERTY_EVENT_LABELS, formatDate } from '../../../utils/labels';

defineProps<{ events: PropertyEvent[] }>();
</script>
