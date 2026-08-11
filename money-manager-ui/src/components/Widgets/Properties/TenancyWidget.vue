<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('property.tenancy.title') }}</h2>

    <div v-if="active" class="p-3 rounded-lg bg-primary-soft border border-border mb-4">
      <p class="font-medium text-primary-strong">{{ active.tenantName }}</p>
      <p class="text-sm text-text tabular-nums">
        {{
          t('property.tenancy.rentLine', {
            rent: formatMoney(active.monthlyRent, active.currencyCode),
            day: active.rentDueDayOfMonth,
          })
        }}
      </p>
      <p class="text-xs text-text-muted">
        {{
          active.endDate
            ? t('property.tenancy.sinceUntil', {
                start: formatDate(active.startDate),
                end: formatDate(active.endDate),
              })
            : t('property.tenancy.since', { start: formatDate(active.startDate) })
        }}
      </p>
    </div>
    <p v-else class="text-sm text-text-muted mb-4">
      {{ t('property.tenancy.vacant') }}
    </p>

    <form class="grid grid-cols-2 gap-2 mb-4" @submit.prevent="submit">
      <BaseInput v-model="form.tenantName" :placeholder="t('property.tenancy.tenantName')" class="col-span-2" required />
      <BaseInput v-model="form.startDate" type="date" required />
      <BaseInput v-model="form.endDate" type="date" :placeholder="t('property.tenancy.endOptional')" />
      <BaseInput
        v-model.number="form.monthlyRent"
        type="number"
        min="1"
        :placeholder="t('property.tenancy.monthlyRent')"
        required
      />
      <BaseInput
        v-model.number="form.rentDueDayOfMonth"
        type="number"
        min="1"
        max="28"
        :placeholder="t('property.tenancy.dueDay')"
      />
      <BaseButton type="submit" block class="col-span-2">{{ t('property.tenancy.add') }}</BaseButton>
    </form>

    <ul v-if="past.length">
      <ListRow v-for="lease in past" :key="lease.id">
        <template #title>
          <span class="truncate text-sm">{{ lease.tenantName }}</span>
        </template>
        <template #trailing>
          <span class="text-sm text-text-muted whitespace-nowrap">
            {{ formatDate(lease.startDate) }} – {{ formatDate(lease.endDate) }}
          </span>
        </template>
      </ListRow>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive } from 'vue';
import type { Lease } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatDate } from '../../../utils/labels';
import type { LeaseRequest } from '../../../services/propertyApi';
import BaseInput from '../../ui/BaseInput.vue';
import BaseButton from '../../ui/BaseButton.vue';
import ListRow from '../../ui/ListRow.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps<{ leases: Lease[] }>();
const emit = defineEmits<{ (e: 'create', payload: LeaseRequest): void }>();

const today = new Date().toISOString().split('T')[0];

const active = computed(() =>
  props.leases.find(
    (l) => l.startDate.split('T')[0] <= today && (!l.endDate || l.endDate.split('T')[0] >= today)
  )
);

const past = computed(() => props.leases.filter((l) => l.id !== active.value?.id));

const form = reactive({
  tenantName: '',
  startDate: today,
  endDate: '',
  monthlyRent: null as number | null,
  rentDueDayOfMonth: 1,
});

function submit() {
  if (!form.monthlyRent || form.monthlyRent <= 0) return;

  emit('create', {
    tenantName: form.tenantName,
    startDate: new Date(form.startDate).toISOString(),
    endDate: form.endDate ? new Date(form.endDate).toISOString() : null,
    monthlyRent: form.monthlyRent,
    rentDueDayOfMonth: form.rentDueDayOfMonth || 1,
  });

  form.tenantName = '';
  form.endDate = '';
  form.monthlyRent = null;
}
</script>
