<template>
  <label class="flex flex-col gap-1.5" :class="wrapperClass" :style="wrapperStyle">
    <span v-if="label" class="text-xs font-semibold text-text-muted">{{ label }}</span>
    <select
      v-bind="selectAttrs"
      :value="modelValue"
      class="w-full rounded-md border border-border bg-surface px-3.5 py-2.5 text-sm text-text focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
      @change="onChange"
    >
      <slot />
    </select>
  </label>
</template>

<script setup lang="ts">
import { computed, useAttrs } from 'vue';
import type { ClassValue, StyleValue } from 'vue';

// See BaseInput: class/style go to the <label> so parent layout classes apply to the element
// the parent actually lays out.
defineOptions({ inheritAttrs: false });

const props = withDefaults(
  defineProps<{
    label?: string;
    modelValue?: string | number | null;
    modelModifiers?: { number?: boolean };
  }>(),
  { modelModifiers: () => ({}) },
);

const attrs = useAttrs();
// See BaseInput: Vue types the class binding now, so `unknown` no longer satisfies it.
const wrapperClass = computed(() => attrs.class as ClassValue);
const wrapperStyle = computed(() => attrs.style as StyleValue);
const selectAttrs = computed(() => {
  const { class: _class, style: _style, ...rest } = attrs;
  return rest;
});

const emit = defineEmits<{ (e: 'update:modelValue', value: string | number | null): void }>();

function onChange(event: Event) {
  const raw = (event.target as HTMLSelectElement).value;
  emit('update:modelValue', props.modelModifiers?.number ? Number(raw) : raw);
}
</script>
