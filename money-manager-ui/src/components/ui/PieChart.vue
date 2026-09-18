<template>
  <div class="flex items-center gap-5">
    <div class="w-[88px] h-[88px] rounded-full shrink-0" :style="{ background: gradient }" />
    <ul class="flex flex-col gap-1.5 text-xs">
      <li v-for="slice in slices" :key="slice.label" class="flex items-center gap-2">
        <span class="w-2 h-2 rounded-full shrink-0" :style="{ background: slice.color }" />
        <span>{{ slice.label }} &middot; {{ formatPercent(slice.percent, 0) }}</span>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { formatPercent } from '../../utils/labels';
import { pieChartSlices, pieChartGradient, type PieChartSegment } from '../../utils/pieChart';

const props = defineProps<{ segments: PieChartSegment[] }>();

const slices = computed(() => pieChartSlices(props.segments));
const gradient = computed(() => pieChartGradient(slices.value));
</script>
