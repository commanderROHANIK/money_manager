<template>
  <BaseCard title="Events">
    <div class="grid grid-cols-2 gap-4">
      <StatCard label="Upcoming" :value="upcomingCount" />
      <StatCard label="Past" :value="pastCount" />
    </div>
  </BaseCard>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchUpcomingEvents } from '../../../services/api';
import type { UpcomingEvent } from '../../../models/models';
import BaseCard from '../../ui/BaseCard.vue';
import StatCard from '../../ui/StatCard.vue';

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
