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
      <PastEventsWidget />
    </div>
  </div>
</template>

<script setup lang="ts">
import EventSummaryStatsWidget from '../components/widgets/EventSummaryStatsWidget.vue';
import UpcomingEventsWidget from '../components/widgets/UpcomingEventsWidget.vue';
import PastEventsWidget from '../components/widgets/PastEventsWidget.vue';
</script>

<style scoped>
/* Optional: custom styling if needed */
</style>
