<template>
  <div>
    <h2 class="text-xl font-semibold mb-4">Timeline</h2>

    <p v-if="events.length === 0" class="text-sm text-gray-500">
      Nothing recorded yet.
    </p>

    <ol v-else class="relative border-l border-gray-200 dark:border-gray-600 ml-2 space-y-4">
      <li v-for="event in events" :key="event.id" class="ml-4">
        <span
          class="absolute -left-1.5 w-3 h-3 rounded-full"
          :class="event.isSystemGenerated ? 'bg-blue-400' : 'bg-gray-400'"
        />
        <div class="flex items-baseline justify-between gap-2">
          <p class="font-medium">{{ event.title }}</p>
          <time class="text-xs text-gray-500 whitespace-nowrap">
            {{ formatDate(event.occurredOn) }}
          </time>
        </div>
        <p v-if="event.description" class="text-sm text-gray-500">{{ event.description }}</p>
        <p class="text-[11px] text-gray-400">{{ PROPERTY_EVENT_LABELS[event.type] }}</p>
      </li>
    </ol>
  </div>
</template>

<script setup lang="ts">
import type { PropertyEvent } from '../../../models/models';
import { PROPERTY_EVENT_LABELS, formatDate } from '../../../utils/labels';

defineProps<{ events: PropertyEvent[] }>();
</script>
