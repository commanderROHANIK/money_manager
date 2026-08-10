<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-1">{{ t('property.valuations.title') }}</h2>
    <p class="text-xs text-text-muted mb-3">
      {{ t('property.valuations.subtitle') }}
    </p>

    <form class="flex flex-wrap gap-2 mb-4" @submit.prevent="submit">
      <BaseInput v-model="form.valuedOn" type="date" required />
      <BaseInput
        v-model.number="form.value"
        type="number"
        min="1"
        :placeholder="t('property.valuations.valuePlaceholder')"
        class="w-32"
        required
      />
      <BaseButton type="submit">{{ t('property.valuations.add') }}</BaseButton>
    </form>

    <p v-if="valuations.length === 0" class="text-sm text-text-muted">
      {{ t('property.valuations.empty') }}
    </p>

    <ul v-else class="text-sm max-h-[200px] overflow-y-auto">
      <ListRow v-for="valuation in sorted" :key="valuation.id">
        <template #title>
          <span class="text-text-muted">{{ formatDate(valuation.valuedOn) }}</span>
        </template>
        <template #trailing>
          <span class="font-medium tabular-nums">{{ formatMoney(valuation.value, valuation.currencyCode) }}</span>
        </template>
      </ListRow>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive } from 'vue';
import type { PropertyValuation } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatDate } from '../../../utils/labels';
import BaseInput from '../../ui/BaseInput.vue';
import BaseButton from '../../ui/BaseButton.vue';
import ListRow from '../../ui/ListRow.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps<{ valuations: PropertyValuation[]; currencyCode: string }>();
const emit = defineEmits<{ (e: 'create', payload: { valuedOn: string; value: number }): void }>();

const sorted = computed(() =>
  [...props.valuations].sort((a, b) => b.valuedOn.localeCompare(a.valuedOn))
);

const form = reactive({
  valuedOn: new Date().toISOString().split('T')[0],
  value: null as number | null,
});

function submit() {
  if (!form.value || form.value <= 0) return;

  emit('create', {
    valuedOn: new Date(form.valuedOn).toISOString(),
    value: form.value,
  });

  form.value = null;
}
</script>
