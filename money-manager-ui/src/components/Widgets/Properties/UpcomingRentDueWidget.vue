<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('property.upcomingRent.title') }}</h2>
    <ul v-if="upcomingRents.length > 0">
      <ListRow v-for="property in upcomingRents" :key="property.id">
        <template #title>
          <span class="font-medium">{{ property.propertyName }}</span>
        </template>
        <template #subtitle>
          <span class="text-sm text-text-muted">
            {{ formatDueDate(property.rentDueDate) }}
          </span>
        </template>
        <template #trailing>
          <span class="text-primary-strong font-semibold tabular-nums">
            {{ formatMoney(property.rentAmount, property.currencyCode) }}
          </span>
        </template>
      </ListRow>
    </ul>
    <p v-else class="text-text-muted">{{ t('property.upcomingRent.none') }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { RentalProperty } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import ListRow from '../../ui/ListRow.vue';
import { useI18n } from 'vue-i18n';
import { intlLocale } from '../../../i18n/locale';

const { t } = useI18n();

const props = defineProps<{
  properties: RentalProperty[];
}>();

// A vacant property has no next rent date at all, so this has to cope with null rather
// than turning it into an Invalid Date.
function formatDueDate(iso: string | null | undefined): string {
  if (!iso) return t('property.upcomingRent.noTenancy');
  return new Date(iso).toLocaleDateString(intlLocale(), {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

const today = new Date();
const inThirtyDays = new Date();
inThirtyDays.setDate(today.getDate() + 30);

const upcomingRents = computed(() =>
  props.properties
    .filter((p) => {
      if (!p.rentDueDate) return false;
      const due = new Date(p.rentDueDate);
      return due >= today && due <= inThirtyDays;
    })
    .sort(
      (a, b) => new Date(a.rentDueDate!).getTime() - new Date(b.rentDueDate!).getTime()
    )
);
</script>

<style scoped>
ul {
  max-height: 300px;
  overflow-y: auto;
}
</style>
