<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('property.ledger.title') }}</h2>

    <form class="flex flex-wrap gap-2 mb-4" @submit.prevent="submit">
      <BaseInput v-model="form.date" type="date" required />
      <BaseSelect v-model.number="form.category">
        <optgroup v-for="group in TRANSACTION_CATEGORY_GROUPS" :key="group.label" :label="group.label">
          <option v-for="category in group.categories" :key="category" :value="category">
            {{ TRANSACTION_CATEGORY_LABELS[category] }}
          </option>
        </optgroup>
      </BaseSelect>
      <BaseInput
        v-model.number="form.amount"
        type="number"
        step="0.01"
        min="0.01"
        :placeholder="t('property.ledger.amount')"
        class="w-32"
        required
      />
      <BaseInput v-model="form.description" :placeholder="t('property.ledger.note')" class="flex-1 min-w-[120px]" />
      <BaseButton type="submit">{{ t('property.ledger.record') }}</BaseButton>
    </form>

    <p class="text-xs text-text-muted mb-3">
      {{ t('property.ledger.positiveHint') }}
    </p>

    <p v-if="transactions.length === 0" class="text-sm text-text-muted">
      {{ t('property.ledger.empty') }}
    </p>

    <ul v-else class="max-h-[320px] overflow-y-auto">
      <ListRow v-for="entry in transactions" :key="entry.id">
        <template #title>
          <p class="text-sm font-medium truncate">
            {{ TRANSACTION_CATEGORY_LABELS[entry.category] }}
          </p>
        </template>
        <template #subtitle>
          <p class="text-xs text-text-muted truncate">
            {{ formatDate(entry.date) }}<span v-if="entry.description"> · {{ entry.description }}</span>
          </p>
        </template>
        <template #trailing>
          <span class="tabular-nums" :class="isIncome(entry.category) ? 'text-primary-strong' : 'text-danger'">
            {{ isIncome(entry.category) ? '+' : '−' }}{{ formatMoney(entry.amount, entry.currencyCode) }}
          </span>
          <button
            class="text-text-muted hover:text-danger text-sm"
            :aria-label="
              t('property.ledger.deleteAria', {
                category: TRANSACTION_CATEGORY_LABELS[entry.category],
              })
            "
            @click="emit('delete', entry.id)"
          >
            ✕
          </button>
        </template>
      </ListRow>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import { TransactionCategory, type PropertyTransaction } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import {
  TRANSACTION_CATEGORY_GROUPS,
  TRANSACTION_CATEGORY_LABELS,
  formatDate,
  isIncome,
} from '../../../utils/labels';
import BaseInput from '../../ui/BaseInput.vue';
import BaseSelect from '../../ui/BaseSelect.vue';
import BaseButton from '../../ui/BaseButton.vue';
import ListRow from '../../ui/ListRow.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

defineProps<{ transactions: PropertyTransaction[] }>();

const emit = defineEmits<{
  (e: 'create', payload: { date: string; amount: number; category: number; description: string }): void;
  (e: 'delete', id: number): void;
}>();

const form = reactive({
  date: new Date().toISOString().split('T')[0],
  amount: null as number | null,
  category: TransactionCategory.RentIncome as number,
  description: '',
});

function submit() {
  if (!form.amount || form.amount <= 0) return;

  emit('create', {
    date: new Date(form.date).toISOString(),
    amount: form.amount,
    category: form.category,
    description: form.description,
  });

  form.amount = null;
  form.description = '';
}
</script>
