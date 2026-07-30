<template>
  <label class="flex flex-col gap-1.5">
    <span v-if="label" class="text-xs font-semibold" :class="error ? 'text-danger' : 'text-text-muted'">
      {{ label }}
    </span>
    <input
      v-bind="$attrs"
      :type="type"
      :value="modelValue ?? ''"
      :placeholder="placeholder"
      class="rounded-md border bg-surface px-3.5 py-2.5 text-sm text-text placeholder:text-text-muted focus:outline-none focus:ring-2"
      :class="error ? 'border-danger focus:ring-danger/25' : 'border-border focus:border-primary focus:ring-primary/20'"
      @input="onInput"
    />
    <span v-if="error" class="text-xs text-danger">{{ error }}</span>
  </label>
</template>

<script setup lang="ts">
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
