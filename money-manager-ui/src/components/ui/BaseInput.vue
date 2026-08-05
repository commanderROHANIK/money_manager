<template>
  <label class="flex flex-col gap-1.5" :class="wrapperClass" :style="wrapperStyle">
    <span v-if="label" class="text-xs font-semibold" :class="error ? 'text-danger' : 'text-text-muted'">
      {{ label }}
    </span>
    <input
      v-bind="inputAttrs"
      :type="type"
      :value="modelValue ?? ''"
      :placeholder="placeholder"
      class="w-full rounded-md border bg-surface px-3.5 py-2.5 text-sm text-text placeholder:text-text-muted focus:outline-none focus:ring-2"
      :class="error ? 'border-danger focus:ring-danger/25' : 'border-border focus:border-primary focus:ring-primary/20'"
      @input="onInput"
    />
    <span v-if="error" class="text-xs text-danger">{{ error }}</span>
  </label>
</template>

<script setup lang="ts">
import { computed, useAttrs } from 'vue';
import type { StyleValue } from 'vue';

// class/style are applied to the <label>, not the <input>: the label is the element the parent
// lays out (grid cell, flex item), so `col-span-2` / `flex-1` have to land there to do anything.
// Every other attr (required, min, step, autocomplete, aria-*) belongs on the control itself.
defineOptions({ inheritAttrs: false });

const props = withDefaults(
  defineProps<{
    label?: string;
    placeholder?: string;
    error?: string;
    type?: string;
    modelValue?: string | number | null;
    modelModifiers?: { number?: boolean; trim?: boolean };
  }>(),
  { modelModifiers: () => ({}) },
);

const attrs = useAttrs();
const wrapperClass = computed(() => attrs.class as unknown);
const wrapperStyle = computed(() => attrs.style as StyleValue);
const inputAttrs = computed(() => {
  const { class: _class, style: _style, ...rest } = attrs;
  return rest;
});

const emit = defineEmits<{ (e: 'update:modelValue', value: string | number | null): void }>();

function onInput(event: Event) {
  let raw = (event.target as HTMLInputElement).value;
  if (props.modelModifiers?.trim) raw = raw.trim();
  if (props.modelModifiers?.number) {
    emit('update:modelValue', raw === '' ? null : Number(raw));
  } else {
    emit('update:modelValue', raw);
  }
}
</script>
