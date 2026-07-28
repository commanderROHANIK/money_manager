<template>
  <div>
    <h2 class="text-xl font-semibold mb-4">Tenancies</h2>

    <div v-if="active" class="p-3 rounded-lg bg-green-50 border border-green-200 mb-4">
      <p class="font-medium text-green-900">{{ active.tenantName }}</p>
      <p class="text-sm text-green-800">
        {{ formatMoney(active.monthlyRent, active.currencyCode) }} / month, due on day
        {{ active.rentDueDayOfMonth }}
      </p>
      <p class="text-xs text-green-700">
        Since {{ formatDate(active.startDate) }}<span v-if="active.endDate"> until {{ formatDate(active.endDate) }}</span>
      </p>
    </div>
    <p v-else class="text-sm text-amber-700 mb-4">
      Vacant — no tenancy is running today.
    </p>

    <form @submit.prevent="submit" class="grid grid-cols-2 gap-2 mb-4">
      <input v-model="form.tenantName" placeholder="Tenant name" class="p-2 border rounded col-span-2" required />
      <input v-model="form.startDate" type="date" class="p-2 border rounded" required />
      <input v-model="form.endDate" type="date" class="p-2 border rounded" placeholder="End (optional)" />
      <input
        v-model.number="form.monthlyRent"
        type="number"
        min="1"
        placeholder="Monthly rent"
        class="p-2 border rounded"
        required
      />
      <input
        v-model.number="form.rentDueDayOfMonth"
        type="number"
        min="1"
        max="28"
        placeholder="Due day"
        class="p-2 border rounded"
      />
      <button type="submit" class="col-span-2 bg-green-600 hover:bg-green-700 text-white py-2 rounded">
        Add tenancy
      </button>
    </form>

    <ul v-if="past.length" class="divide-y text-sm">
      <li v-for="lease in past" :key="lease.id" class="py-2 flex justify-between">
        <span class="truncate">{{ lease.tenantName }}</span>
        <span class="text-gray-500 whitespace-nowrap">
          {{ formatDate(lease.startDate) }} – {{ formatDate(lease.endDate) }}
        </span>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive } from 'vue';
import type { Lease } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import { formatDate } from '../../../utils/labels';
import type { LeaseRequest } from '../../../services/propertyApi';

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
