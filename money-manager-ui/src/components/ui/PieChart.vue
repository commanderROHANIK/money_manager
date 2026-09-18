<template>
  <div class="flex items-center gap-5">
    <div class="relative w-[88px] h-[88px] shrink-0">
      <svg viewBox="0 0 100 100" class="w-full h-full -rotate-90" aria-hidden="true">
        <circle
          v-if="ring.segments.length === 0"
          cx="50"
          cy="50"
          :r="ring.radius"
          fill="none"
          class="stroke-border"
          :stroke-width="STROKE_WIDTH"
        />
        <circle
          v-for="segment in ring.segments"
          :key="segment.label"
          cx="50"
          cy="50"
          :r="ring.radius"
          fill="none"
          :stroke="segment.color"
          :stroke-width="STROKE_WIDTH"
          stroke-linecap="round"
          :stroke-dasharray="segment.dashArray"
          :stroke-dashoffset="segment.dashOffset"
        />
      </svg>
      <div v-if="$slots.center" class="absolute inset-0 flex flex-col items-center justify-center text-center">
        <slot name="center" />
      </div>
    </div>
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
import { pieChartSlices, pieChartRing, type PieChartSegment } from '../../utils/pieChart';

// gapLength (see pieChart.ts) must stay greater than this or rounded caps swallow the gap.
const STROKE_WIDTH = 10;

const props = defineProps<{ segments: PieChartSegment[] }>();

const slices = computed(() => pieChartSlices(props.segments));
const ring = computed(() => pieChartRing(slices.value));
</script>
